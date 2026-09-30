using MediatR;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Application.Admin.Commands.ToggleUserAccountStatus;

public class ToggleUserAccountStatusCommandHandler : IRequestHandler<ToggleUserAccountStatusCommand, UserAccountDto>
{
    private readonly IIdentityService _identityService;

    public ToggleUserAccountStatusCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<UserAccountDto> Handle(ToggleUserAccountStatusCommand request, CancellationToken cancellationToken)
    {
        return await _identityService.ToggleUserAccountStatusAsync(request.UserId, request.EnableAccount, cancellationToken);
    }
}
