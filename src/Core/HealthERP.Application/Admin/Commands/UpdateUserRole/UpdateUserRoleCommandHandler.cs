using MediatR;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Application.Admin.Commands.UpdateUserRole;

public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand, UserAccountDto>
{
    private readonly IIdentityService _identityService;

    public UpdateUserRoleCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<UserAccountDto> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        return await _identityService.UpdateUserRoleAsync(request.UserId, request.RoleName, cancellationToken);
    }
}
