namespace HealthERP.Application.StaffMembers.Models;

public sealed record StaffLinkedAccountDto(Guid UserId, string FirstName, string LastName, string? Email);
public sealed record StaffAccountCandidateDto(Guid UserId, string FirstName, string LastName, string? Email,
    List<string> Roles, bool IsActive, bool IsLinkedToStaff);
public sealed record StaffAccountLookupDto(List<StaffAccountCandidateDto> Items, int Page, int PageSize, bool HasMore);
public sealed record StaffAccountLinkDto(Guid StaffId, string StaffKind, StaffLinkedAccountDto? LinkedAccount);
