using MediatR;

namespace HealthERP.Application.Authentication.Commands.ResetPassword;

public record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword
) : IRequest<ResetPasswordResponse>;

public record ResetPasswordResponse(
    bool Success,
    string Message,
    string[] Errors
);
