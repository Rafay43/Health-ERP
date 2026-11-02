using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.StaffMembers.Models;
using MediatR;

namespace HealthERP.Application.StaffMembers.Queries.LookupStaffAccounts;

public record LookupStaffAccountsQuery(string? Search = null, int Page = 1, int PageSize = 20) : IRequest<StaffAccountLookupDto>;
public sealed class LookupStaffAccountsQueryHandler(IStaffAccountService accounts) : IRequestHandler<LookupStaffAccountsQuery, StaffAccountLookupDto>
{
    public Task<StaffAccountLookupDto> Handle(LookupStaffAccountsQuery request, CancellationToken ct)
        => accounts.LookupAsync(request.Search, request.Page, request.PageSize, ct);
}
