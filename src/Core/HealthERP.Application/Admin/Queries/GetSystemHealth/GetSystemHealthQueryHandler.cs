using MediatR;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Application.Admin.Queries.GetSystemHealth;

public class GetSystemHealthQueryHandler : IRequestHandler<GetSystemHealthQuery, SystemHealthDto>
{
    private readonly IIdentityService _identityService;

    public GetSystemHealthQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<SystemHealthDto> Handle(GetSystemHealthQuery request, CancellationToken cancellationToken)
    {
        return await _identityService.GetSystemHealthAsync(cancellationToken);
    }
}
