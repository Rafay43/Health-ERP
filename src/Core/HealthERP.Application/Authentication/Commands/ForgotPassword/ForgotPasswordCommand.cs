using MediatR;

namespace HealthERP.Application.Authentication.Commands.ForgotPassword;

public record ForgotPasswordCommand(
    string Email
) : IRequest<ForgotPasswordResponse>;

public record ForgotPasswordResponse(
    string Message
);
