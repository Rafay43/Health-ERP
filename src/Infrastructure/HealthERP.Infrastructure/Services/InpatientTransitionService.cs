using System.Data;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Inpatient;
using HealthERP.Application.Inpatient.Models;
using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Triage;
using HealthERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HealthERP.Infrastructure.Services;

public sealed class InpatientTransitionService(ApplicationDbContext context) : IInpatientTransitionService
{
    public Task<Guid> AdmitAsync(Guid patientId, Guid bedId, string doctor, string complaint,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
    {
        var patient = await LoadPatient(patientId, cancellationToken);
        var admission = await Acquire(patient.Id, bedId, doctor, complaint, cancellationToken);
        return admission.Id.Value;
    }, cancellationToken);

    public Task<Guid> AdmitFromEmergencyAsync(Guid triageId, Guid bedId, string doctor, string notes,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
    {
        var id = new TriageId(triageId);
        var triage = await context.TriageRecords.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Triage record was not found.");
        triage = TrackSnapshot(triage);
        if (triage.Status is TriageStatus.DischargedFromED or TriageStatus.TransferredToInpatient)
            throw new InpatientConflictException("This Emergency record has already been transferred or discharged.");
        await LoadPatient(triage.PatientId.Value, cancellationToken);
        var admission = await Acquire(triage.PatientId, bedId, doctor,
            $"[ER Transfer] {notes} (Original Complaint: {triage.ChiefComplaint})", cancellationToken);
        triage.TransferToInpatient(admission.BedId);
        return admission.Id.Value;
    }, cancellationToken);

    public Task<AdmissionDto> TransferAsync(Guid admissionId, Guid destinationId, string? reason,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
    {
        var admission = await LoadAdmission(admissionId, cancellationToken);
        if (admission.IsDischarged) throw new InpatientConflictException("Cannot transfer a discharged patient.");
        if (admission.BedId.Value == destinationId) throw new InpatientConflictException("Patient is already assigned to this bed.");
        var source = await LoadBed(admission.BedId.Value, cancellationToken);
        var destination = await LoadBed(destinationId, cancellationToken);
        await RequireOwnedSource(admission, source, cancellationToken);
        await RequireAvailable(destination, cancellationToken);
        var patient = await LoadPatient(admission.PatientId.Value, cancellationToken);

        // All validation precedes mutation; all three mutations share one save/commit.
        admission.TransferBed(destination.Id, destination.DailyRate, reason);
        source.Release(); destination.Occupy();
        return Dto(admission, patient, destination);
    }, cancellationToken);

    public Task<AdmissionDto> DischargeAsync(Guid admissionId, string summary,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
    {
        var admission = await LoadAdmission(admissionId, cancellationToken);
        if (admission.IsDischarged) throw new InpatientConflictException("Patient has already been discharged.");
        var bed = await LoadBed(admission.BedId.Value, cancellationToken);
        await RequireOwnedSource(admission, bed, cancellationToken);
        var patient = await LoadPatient(admission.PatientId.Value, cancellationToken);
        admission.Discharge(summary); bed.Release();
        return Dto(admission, patient, bed);
    }, cancellationToken);

    private async Task<Admission> Acquire(PatientId patientId, Guid bedId, string doctor, string complaint, CancellationToken ct)
    {
        // Serializable protects this absence predicate even when two requests choose different beds.
        if (await context.Admissions.AnyAsync(a => a.PatientId == patientId && !a.IsDischarged, ct))
            throw new InpatientConflictException("Patient already has an active inpatient admission.");
        var bed = await LoadBed(bedId, ct);
        await RequireAvailable(bed, ct);
        var admission = Admission.Create(patientId, bed.Id, doctor, complaint, bed.DailyRate);
        bed.Occupy(); context.Admissions.Add(admission);
        return admission;
    }

    private async Task RequireAvailable(Bed bed, CancellationToken ct)
    {
        if (bed.Status != BedStatus.Available)
            throw new InpatientConflictException("The selected bed is not available. Refresh beds before retrying.");
        if (await context.Admissions.AnyAsync(a => a.BedId == bed.Id && !a.IsDischarged, ct))
            throw new InpatientConflictException("The selected bed already has an active admission. Refresh beds before retrying.");
    }

    private async Task RequireOwnedSource(Admission admission, Bed bed, CancellationToken ct)
    {
        if (bed.Status != BedStatus.Occupied
            || await context.Admissions.AnyAsync(a => a.BedId == bed.Id && !a.IsDischarged && a.Id != admission.Id, ct))
            throw new InpatientConflictException("Admission and bed occupancy are inconsistent. Refresh and reconcile before retrying.");
    }

    private async Task<Admission> LoadAdmission(Guid value, CancellationToken ct)
    {
        var id = new AdmissionId(value);
        var admission = await context.Admissions.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException("Admission was not found.");
        return TrackSnapshot(admission);
    }

    private async Task<Bed> LoadBed(Guid value, CancellationToken ct)
    {
        var id = new BedId(value);
        var bed = await context.Beds.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new KeyNotFoundException("Bed was not found.");
        return TrackSnapshot(bed);
    }

    private async Task<Patient> LoadPatient(Guid value, CancellationToken ct)
    {
        var id = new PatientId(value);
        return await context.Patients.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Patient was not found.");
    }

    private T TrackSnapshot<T>(T snapshot) where T : class
    {
        var key = context.Entry(snapshot).Metadata.FindPrimaryKey()!;
        var tracked = context.ChangeTracker.Entries<T>().SingleOrDefault(e => key.Properties.All(p =>
            Equals(e.Property(p.Name).CurrentValue, context.Entry(snapshot).Property(p.Name).CurrentValue)));
        // Replace the entry, including its relationship snapshot. Copying scalars into an old
        // tracked Admission can let EF's stale Bed navigation restore the earlier foreign key.
        if (tracked is not null) tracked.State = EntityState.Detached;
        context.Attach(snapshot);
        return snapshot;
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        // Begin before ANY authoritative read. No automatic retry: a losing hospital action is a conflict.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await operation();
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Exception exception)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch { /* Preserve the original failure if the connection also fails during rollback. */ }
            finally { context.ChangeTracker.Clear(); }
            var causes = ExceptionChain(exception).ToArray();
            if (causes.Any(e => e is DbUpdateConcurrencyException or PostgresException { SqlState: "40001" or "40P01" }))
                throw new InpatientConflictException("Bed or admission changed during this operation. Refresh beds and admissions before retrying.");
            if (causes.Any(e => e is DbUpdateException or NpgsqlException))
                throw new InpatientStorageException(exception);
            throw;
        }
    }

    private static IEnumerable<Exception> ExceptionChain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException) yield return current;
    }

    private static AdmissionDto Dto(Admission a, Patient p, Bed b) => new(a.Id.Value, a.PatientId.Value,
        $"{p.FirstName} {p.LastName}", p.MedicalRecordNumber, a.BedId.Value, b.WardName, b.RoomNumber, b.BedNumber,
        a.AdmittingDoctorName, a.ChiefComplaint, a.AdmissionDateUtc, a.DischargeDateUtc, a.DischargeSummary,
        a.DailyRateSnapshot, a.TotalRoomCharge, a.IsDischarged);
}
