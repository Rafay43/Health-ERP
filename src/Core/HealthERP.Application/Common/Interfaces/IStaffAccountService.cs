using HealthERP.Application.StaffMembers.Models;
using HealthERP.Domain.StaffMembers;

namespace HealthERP.Application.Common.Interfaces;

public interface IStaffAccountService
{
    Task<StaffAccountLookupDto> LookupAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, StaffLinkedAccountDto>> GetLinkedAccountsAsync(IEnumerable<Guid> staffIds, CancellationToken ct = default);
    Task<StaffAccountLinkDto> LinkAsync(Guid staffId, Guid userId, StaffKind kind, CancellationToken ct = default);
    Task UnlinkAsync(Guid staffId, CancellationToken ct = default);
}
