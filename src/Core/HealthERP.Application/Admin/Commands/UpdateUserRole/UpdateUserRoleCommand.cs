using MediatR;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Application.Admin.Commands.UpdateUserRole;

public record UpdateUserRoleCommand(
    Guid UserId,
    string RoleName
) : IRequest<UserAccountDto>;
