using HealthERP.Application.StaffMembers.Models;

namespace HealthERP.Application.Common.Interfaces;

public interface ICurrentStaffService
{
    // Both methods fail closed: there is no caller-selected account or Staff identifier.
    Task<CurrentStaffContext> GetCurrentStaffAsync(CancellationToken cancellationToken = default);
    Task<CurrentStaffContext> GetCurrentDoctorAsync(CancellationToken cancellationToken = default);
}
