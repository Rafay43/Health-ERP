using MediatR;
using HealthERP.Application.Authentication.Models;
using HealthERP.Application.Common.Interfaces;

namespace HealthERP.Application.Authentication.Commands.RegisterUser;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, AuthResponse>
{
    private readonly IIdentityService _identityService;

    public RegisterUserCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<AuthResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var (success, response, errors) = await _identityService.RegisterAsync(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            cancellationToken);

        if (!success || response is null)
        {
            throw new InvalidOperationException(string.Join("; ", errors));
        }

        return response;
    }
}
