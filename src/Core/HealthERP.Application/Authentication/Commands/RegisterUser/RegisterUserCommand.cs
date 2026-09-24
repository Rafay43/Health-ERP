using MediatR;
using HealthERP.Application.Authentication.Models;

namespace HealthERP.Application.Authentication.Commands.RegisterUser;

public record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName
) : IRequest<AuthResponse>;
