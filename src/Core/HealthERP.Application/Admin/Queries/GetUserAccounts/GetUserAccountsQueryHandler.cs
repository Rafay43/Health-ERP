using MediatR;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Application.Admin.Queries.GetUserAccounts;

public class GetUserAccountsQueryHandler : IRequestHandler<GetUserAccountsQuery, List<UserAccountDto>>
{
    private readonly IIdentityService _identityService;

    public GetUserAccountsQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<List<UserAccountDto>> Handle(GetUserAccountsQuery request, CancellationToken cancellationToken)
    {
        return await _identityService.GetUserAccountsAsync(cancellationToken);
    }
}
