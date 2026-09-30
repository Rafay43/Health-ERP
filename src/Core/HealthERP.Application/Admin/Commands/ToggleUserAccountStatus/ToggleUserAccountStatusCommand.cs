using MediatR;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Application.Admin.Commands.ToggleUserAccountStatus;

public record ToggleUserAccountStatusCommand(
    Guid UserId,
    bool EnableAccount
) : IRequest<UserAccountDto>;
