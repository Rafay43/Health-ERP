using MediatR;
using HealthERP.Application.Authentication.Models;

namespace HealthERP.Application.Authentication.Commands.Login;

public record LoginCommand(
    string Email,
    string Password
) : IRequest<AuthResponse>;
