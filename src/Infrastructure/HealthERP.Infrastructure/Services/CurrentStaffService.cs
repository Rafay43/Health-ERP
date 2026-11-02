using System.Security.Claims;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Security;
using HealthERP.Application.StaffMembers;
using HealthERP.Application.StaffMembers.Models;
using HealthERP.Domain.StaffMembers;
using HealthERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HealthERP.Infrastructure.Services;

public sealed class CurrentStaffService(ApplicationDbContext context, ICurrentUserService currentUser,
    IHttpContextAccessor httpContext, ILogger<CurrentStaffService> logger) : ICurrentStaffService
{
    public Task<CurrentStaffContext> GetCurrentStaffAsync(CancellationToken cancellationToken = default)
        => ResolveAsync(requireDoctor: false, cancellationToken);

    public Task<CurrentStaffContext> GetCurrentDoctorAsync(CancellationToken cancellationToken = default)
        => ResolveAsync(requireDoctor: true, cancellationToken);

    private async Task<CurrentStaffContext> ResolveAsync(bool requireDoctor, CancellationToken ct)
    {
        // JWT signature/lifetime/session_version validation belongs to the existing middleware.
        // Check authenticated identity here as well, so background/anonymous calls cannot resolve Staff.
        var principal = httpContext.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(currentUser.UserId, out var userId) || userId == Guid.Empty
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var principalId) || principalId != userId)
            throw new UnauthorizedAccessException();

        // Live reads also reject account/Staff changes since authentication ran. No cross-request cache.
        var account = await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (account is null || !account.IsActive
            || (account.LockoutEnabled && account.LockoutEnd >= DateTimeOffset.UtcNow))
            throw new StaffAccessDeniedException();

        var matches = await context.Staff.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.UserId == userId).Take(2).ToListAsync(ct);
        if (matches.Count != 1)
        {
            if (matches.Count > 1) logger.LogError("Multiple Staff associations exist for an authenticated account. Access denied.");
            throw new StaffAccessDeniedException();
        }
        var staff = matches[0];
        if (!staff.IsActive || staff.IsDeleted || !Enum.IsDefined(staff.StaffKind))
            throw new StaffAccessDeniedException();

        if (requireDoctor)
        {
            var isDoctor = await context.UserRoles.Join(context.Roles,
                membership => membership.RoleId, role => role.Id,
                (membership, role) => new { membership.UserId, role.Name })
                .AnyAsync(r => r.UserId == userId && r.Name == Roles.Doctor, ct);
            if (!isDoctor || staff.StaffKind != StaffKind.Doctor) throw new StaffAccessDeniedException();
        }
        return new CurrentStaffContext(staff.Id, userId, staff.StaffKind);
    }
}
