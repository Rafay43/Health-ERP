using MediatR;
using HealthERP.Application.Authentication.Models;
using HealthERP.Application.Common.Interfaces;

namespace HealthERP.Application.Authentication.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IIdentityService _identityService;

    public LoginCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var (success, response, errors) = await _identityService.LoginAsync(
            request.Email,
            request.Password,
            cancellationToken);

        if (!success || response is null)
        {
            throw new InvalidOperationException(string.Join("; ", errors));
        }

        return response;
    }
}
