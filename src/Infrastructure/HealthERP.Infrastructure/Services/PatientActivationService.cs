using System.Data;
using System.Security.Cryptography;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientAccount;
using HealthERP.Application.PatientAccount.Models;
using HealthERP.Domain.Patients;
using HealthERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HealthERP.Infrastructure.Services;

public sealed class PatientActivationService(ApplicationDbContext context, ICurrentUserService currentUser, IEmailService email)
    : IPatientActivationService
{
    private const string GenericMessage = "If the provided information matches an eligible patient record on file, a verification code has been sent to the contact email.";
    private sealed record Issuance(StartActivationResponseDto Response, string? Email = null, string? Name = null, string? Code = null);

    public async Task<StartActivationResponseDto> StartAsync(string mrn, DateOnly dateOfBirth, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mrn)) throw new ArgumentException("Medical record number is required.");
        var userId = UserId();
        var result = await ProtectedAsync(userId, async () =>
        {
            if (await context.Patients.AnyAsync(p => p.UserId == userId, cancellationToken))
                return new Issuance(new(false, "Account is already linked to a Patient record."));
            var normalized = mrn.Trim().ToUpperInvariant();
            var patient = await context.Patients
                .FromSqlInterpolated($"SELECT *, xmin FROM patients.patients WHERE UPPER(\"MedicalRecordNumber\") = {normalized} AND \"DateOfBirth\" = {dateOfBirth} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (patient is null || patient.UserId is not null)
                return new Issuance(new(true, GenericMessage, ResendCooldownUntilUtc: DateTime.UtcNow.AddSeconds(60)));
            if (string.IsNullOrWhiteSpace(patient.Email))
                return new Issuance(new(false, "Portal activation cannot be completed online because no verified email contact is registered on this patient record. Please contact hospital support.", RequiresSupport: true));

            var history = await History(userId, cancellationToken);
            var latest = history.FirstOrDefault();
            // The code-only verify contract has one current flow per authenticated account.
            // Issuance history includes consumed/exhausted rows and all candidate patients.
            if (latest?.ResendCooldownUntilUtc is DateTime until && DateTime.UtcNow < until)
                return new Issuance(new(true, "A verification code was recently requested. Check your inbox or wait before requesting another code.",
                    ResendCooldownUntilUtc: until));
            foreach (var older in history) older.MarkConsumed();
            // A replacement must not accidentally reuse a still-unexpired older numeric code.
            var oldHashes = history.Where(c => c.ExpiresAtUtc >= DateTime.UtcNow).Select(c => c.CodeHash).ToHashSet();
            string? raw = null;
            for (var attempt = 0; attempt < 64; attempt++)
            {
                var proposed = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
                if (!oldHashes.Contains(PatientActivationChallenge.HashCode(proposed))) { raw = proposed; break; }
            }
            if (raw is null) throw new ActivationConflictException();
            var challenge = PatientActivationChallenge.Create(userId, patient.Id, raw, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(60));
            context.PatientActivationChallenges.Add(challenge);
            return new Issuance(new(true, GenericMessage, Mask(patient.Email), ResendCooldownUntilUtc: challenge.ResendCooldownUntilUtc),
                patient.Email, $"{patient.FirstName} {patient.LastName}".Trim(), raw);
        }, cancellationToken);

        // ProtectedAsync commits AND disposes the transaction before any external delivery.
        if (result.Email is null) return result.Response;
        try { await email.SendPatientActivationCodeEmailAsync(result.Email, result.Name!, result.Code!, cancellationToken); }
        catch (ActivationEmailDeliveryException)
        {
            return result.Response with { Success = false, Message = "Unable to deliver a verification code at this time. Wait until the cooldown ends before requesting another code, or contact hospital support." };
        }
        return result.Response;
    }

    public Task<VerifyActivationResponseDto> VerifyAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 6 || code.Trim().Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Verification code must contain six digits.");
        var userId = UserId();
        return ProtectedAsync(userId, async () =>
        {
            if (await context.Patients.AnyAsync(p => p.UserId == userId, cancellationToken))
                return new VerifyActivationResponseDto(false, "Account is already linked to a Patient record.");

            // Discover only the candidate identifier while holding the user anchor; it is a hint,
            // not a verification decision. Lock Patient before loading authoritative challenge state.
            var hint = await context.PatientActivationChallenges.AsNoTracking().Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAtUtc).ThenByDescending(c => c.Id).FirstOrDefaultAsync(cancellationToken);
            if (hint is null) return new(false, "No active activation request found. Please start activation again.");
            var patientId = hint.PatientId.Value;
            foreach (var entry in context.ChangeTracker.Entries<Patient>().Where(e => e.Entity.Id.Value == patientId).ToArray())
                entry.State = EntityState.Detached;
            var patient = await context.Patients
                .FromSqlInterpolated($"SELECT *, xmin FROM patients.patients WHERE \"Id\" = {patientId} FOR UPDATE")
                .AsTracking().SingleOrDefaultAsync(cancellationToken);
            var history = await History(userId, cancellationToken);
            var challenge = history[0];
            // Latest issuance is authoritative even when consumed. Never fall back to legacy rows.
            foreach (var older in history.Skip(1)) older.MarkConsumed();
            if (challenge.IsConsumed) return new(false, "No active activation request found. Please start activation again.");
            if (DateTime.UtcNow > challenge.ExpiresAtUtc)
            {
                challenge.MarkConsumed(); return new(false, "Verification code has expired. Please request a new code.");
            }
            if (challenge.AttemptCount >= 5)
            {
                challenge.MarkConsumed(); return new(false, "Maximum verification attempts exceeded. Please restart activation.");
            }
            if (patient is null || patient.UserId is not null)
                return new(false, "This patient record is no longer eligible for online activation. Please contact hospital support.");
            if (!challenge.VerifyCode(code))
                return new(false, challenge.AttemptCount >= 5
                    ? "Maximum verification attempts exceeded. Please restart activation."
                    : "Invalid verification code. Please check your code and try again.");

            // Consumption and linkage share the ONLY save and commit in this operation.
            patient.LinkUserId(userId);
            return new(true, "Patient Portal activated successfully.",
                new LinkedPatientSummaryDto(patient.MedicalRecordNumber, $"{patient.FirstName} {patient.LastName}".Trim()));
        }, cancellationToken);
    }

    private async Task<List<PatientActivationChallenge>> History(Guid userId, CancellationToken ct)
    {
        // Avoid cached attempt/consumption snapshots if a scope is reused.
        foreach (var entry in context.ChangeTracker.Entries<PatientActivationChallenge>().Where(e => e.Entity.UserId == userId).ToArray())
            entry.State = EntityState.Detached;
        return await context.PatientActivationChallenges.Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAtUtc).ThenByDescending(c => c.Id).ToListAsync(ct);
    }

    private async Task<T> ProtectedAsync<T>(Guid userId, Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var anchor = await context.Database.SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM identity.users WHERE \"Id\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (anchor == Guid.Empty) throw new UnauthorizedAccessException();
            var result = await action();
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Exception exception)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch { /* Preserve the original failure if the connection also fails during rollback. */ }
            finally { context.ChangeTracker.Clear(); }
            var causes = Chain(exception).ToArray();
            if (causes.Any(e => e is DbUpdateConcurrencyException or PostgresException { SqlState: "40001" or "40P01" or "23505" }))
                throw new ActivationConflictException();
            if (causes.Any(e => e is DbUpdateException or NpgsqlException)) throw new ActivationStorageException(exception);
            throw;
        }
    }

    private Guid UserId() => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException();
    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException) yield return current;
    }
    private static string Mask(string address)
    {
        var parts = address.Split('@');
        if (parts.Length != 2 || parts[0].Length == 0) return "***@***";
        var name = parts[0];
        return (name.Length <= 2 ? name[0] + "*" : name[0] + new string('*', Math.Min(name.Length - 2, 4)) + name[^1]) + "@" + parts[1];
    }
}
