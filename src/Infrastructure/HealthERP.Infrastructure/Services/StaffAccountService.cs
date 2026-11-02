using System.Data;
using System.Security.Claims;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Security;
using HealthERP.Application.StaffMembers;
using HealthERP.Application.StaffMembers.Models;
using HealthERP.Domain.StaffMembers;
using HealthERP.Infrastructure.Identity;
using HealthERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Staff = HealthERP.Domain.StaffMembers.StaffMembers;

namespace HealthERP.Infrastructure.Services;

public sealed class StaffAccountService(ApplicationDbContext context, UserManager<ApplicationUser> users,
    ICurrentUserService currentUser, IHttpContextAccessor httpContext) : IStaffAccountService
{
    private void RequireAdmin()
    {
        var principal = httpContext.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true || !principal.IsInRole(Roles.Admin)
            || !Guid.TryParse(currentUser.UserId, out var id) || id == Guid.Empty
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var principalId) || principalId != id)
            throw new StaffAccountForbiddenException();
    }

    public async Task<StaffAccountLookupDto> LookupAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        RequireAdmin();
        var term = search?.Trim().ToLowerInvariant();
        if (page < 1 || page > 10000 || pageSize < 1 || pageSize > 50 || term?.Length > 100)
            throw new ArgumentException("Use a page between 1 and 10000, page size between 1 and 50, and search up to 100 characters.");
        var query = context.Users.AsNoTracking();
        if (!string.IsNullOrEmpty(term)) query = query.Where(u =>
            (u.FirstName + " " + u.LastName).ToLower().Contains(term) || (u.Email != null && u.Email.ToLower().Contains(term)));
        var now = DateTimeOffset.UtcNow;
        var rows = await query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email,
                IsActive = u.IsActive && !(u.LockoutEnabled && u.LockoutEnd >= now),
                IsLinked = context.Staff.IgnoreQueryFilters().Any(s => s.UserId == u.Id) }).ToListAsync(ct);
        var ids = rows.Take(pageSize).Select(u => u.Id).ToArray();
        var roles = await context.UserRoles.Join(context.Roles, m => m.RoleId, r => r.Id,
            (m, r) => new { m.UserId, r.Name }).Where(r => ids.Contains(r.UserId)).ToListAsync(ct);
        return new(rows.Take(pageSize).Select(u => new StaffAccountCandidateDto(u.Id, u.FirstName, u.LastName, u.Email,
            roles.Where(r => r.UserId == u.Id && r.Name != null).Select(r => r.Name!).Order().ToList(), u.IsActive, u.IsLinked)).ToList(),
            page, pageSize, rows.Count > pageSize);
    }

    public async Task<IReadOnlyDictionary<Guid, StaffLinkedAccountDto>> GetLinkedAccountsAsync(IEnumerable<Guid> staffIds, CancellationToken ct = default)
    {
        RequireAdmin(); var ids = staffIds.Select(id => new StaffId(id)).ToArray();
        var rows = await context.Staff.AsNoTracking().Where(s => ids.Contains(s.Id))
            .Join(context.Users.AsNoTracking(), s => s.UserId, u => (Guid?)u.Id,
                (s, u) => new { StaffId = s.Id, Account = new StaffLinkedAccountDto(u.Id, u.FirstName, u.LastName, u.Email) }).ToListAsync(ct);
        return rows.ToDictionary(r => r.StaffId.Value, r => r.Account);
    }

    public Task<StaffAccountLinkDto> LinkAsync(Guid staffId, Guid userId, StaffKind kind, CancellationToken ct = default)
    {
        RequireAdmin();
        if (staffId == Guid.Empty || userId == Guid.Empty || !Enum.IsDefined(kind))
            throw new ArgumentException("A valid staff record, account and supported staff classification are required.");
        return ProtectedAsync(async () =>
        {
            var staff = await LockStaff(staffId, ct);
            if (!staff.IsActive) throw new StaffAccountConflictException("Inactive staff cannot be linked.");
            var account = await LockAccount(userId, ct);
            if (!account.IsActive || await users.IsLockedOutAsync(account))
                throw new StaffAccountConflictException("Select an active account.");
            if (kind == StaffKind.Doctor && !await users.IsInRoleAsync(account, Roles.Doctor))
                throw new StaffAccountConflictException("Doctor classification requires an account with the Doctor role. Assign the role separately before linking.");
            if (staff.UserId.HasValue && staff.UserId != userId)
                throw new StaffAccountConflictException("Staff is already linked to another account. Unlink it explicitly before assigning a different account.");
            if (await context.Staff.IgnoreQueryFilters().AnyAsync(s => s.UserId == userId && s.Id != staff.Id, ct))
                throw new StaffAccountConflictException("Account is already linked to another staff record.");
            if (staff.UserId != userId || staff.StaffKind != kind)
            {
                staff.LinkUserId(userId); staff.AssignStaffKind(kind);
                await Revoke(account);
            }
            return new StaffAccountLinkDto(staff.Id.Value, staff.StaffKind.ToString(), Summary(account));
        }, ct);
    }

    public async Task UnlinkAsync(Guid staffId, CancellationToken ct = default)
    {
        RequireAdmin(); if (staffId == Guid.Empty) throw new ArgumentException("A valid staff record is required.");
        await ProtectedAsync(async () =>
        {
            var staff = await LockStaff(staffId, ct);
            var account = staff.UserId.HasValue ? await LockAccount(staff.UserId.Value, ct) : null;
            if (staff.UserId.HasValue || staff.StaffKind != StaffKind.Unspecified)
            {
                staff.UnlinkUserId(); staff.AssignStaffKind(StaffKind.Unspecified);
                if (account is not null) await Revoke(account);
            }
            return true;
        }, ct);
    }

    private async Task<Staff> LockStaff(Guid id, CancellationToken ct)
    {
        foreach (var entry in context.ChangeTracker.Entries<Staff>().Where(e => e.Entity.Id.Value == id).ToArray()) entry.State = EntityState.Detached;
        var staff = await context.Staff.FromSqlInterpolated($"SELECT * FROM staff.staff WHERE \"Id\" = {id} FOR UPDATE")
            .IgnoreQueryFilters().AsTracking().SingleOrDefaultAsync(ct);
        if (staff is null || staff.IsDeleted) throw new KeyNotFoundException("Staff record was not found.");
        return staff;
    }

    private async Task<ApplicationUser> LockAccount(Guid id, CancellationToken ct)
    {
        var anchor = await context.Database.SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM identity.users WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (anchor == Guid.Empty) throw new KeyNotFoundException("Identity account was not found.");
        foreach (var entry in context.ChangeTracker.Entries<ApplicationUser>().Where(e => e.Entity.Id == id).ToArray()) entry.State = EntityState.Detached;
        return await users.FindByIdAsync(id.ToString()) ?? throw new KeyNotFoundException("Identity account was not found.");
    }

    private async Task Revoke(ApplicationUser account)
    {
        // The Identity EF store shares this scoped DbContext/transaction. Its SaveChanges may
        // flush Staff too, but neither association nor stamp becomes visible before our commit.
        var result = await users.UpdateSecurityStampAsync(account);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "ConcurrencyFailure"))
                throw new StaffAccountConflictException("Account state changed. Refresh before retrying.");
            throw new StaffAccountStorageException(new InvalidOperationException("Identity stamp update failed."));
        }
    }

    private async Task<T> ProtectedAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var result = await operation(); await context.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
        }
        catch (Exception error)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); } catch { /* Preserve original failure. */ }
            finally { context.ChangeTracker.Clear(); }
            var causes = Chain(error).ToArray();
            if (causes.Any(e => e is DbUpdateConcurrencyException or PostgresException { SqlState: "23505" or "23503" or "40001" or "40P01" }))
                throw new StaffAccountConflictException("Association state changed. Refresh staff and accounts before retrying.");
            if (causes.Any(e => e is DbUpdateException or NpgsqlException)) throw new StaffAccountStorageException(error);
            throw;
        }
    }
    private static IEnumerable<Exception> Chain(Exception error)
    { for (Exception? current = error; current is not null; current = current.InnerException) yield return current; }
    private static StaffLinkedAccountDto Summary(ApplicationUser account) => new(account.Id, account.FirstName, account.LastName, account.Email);
}
