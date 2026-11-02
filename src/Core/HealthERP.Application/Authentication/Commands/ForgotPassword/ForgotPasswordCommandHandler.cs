using MediatR;
using HealthERP.Application.Common.Interfaces;

namespace HealthERP.Application.Authentication.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResponse>
{
    private readonly IIdentityService _identityService;

    public ForgotPasswordCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<ForgotPasswordResponse> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var (_, message) = await _identityService.ForgotPasswordAsync(request.Email, cancellationToken);
        return new ForgotPasswordResponse(message);
    }
}
