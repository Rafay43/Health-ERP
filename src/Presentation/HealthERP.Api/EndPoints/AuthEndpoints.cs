using MediatR;
using HealthERP.Application.Authentication.Commands.RegisterUser;
using HealthERP.Application.Authentication.Commands.Login;
using HealthERP.Application.Authentication.Commands.ForgotPassword;
using HealthERP.Application.Authentication.Commands.ResetPassword;
using HealthERP.Application.Authentication.Models;

namespace HealthERP.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        group.MapPost("/register", async (RegisterUserCommand command, IMediator mediator) =>
        {
            var response = await mediator.Send(command);
            return Results.Ok(response);
        })
        .WithName("RegisterUser")
        .AllowAnonymous()
        .Produces<AuthResponse>(200)
        .Produces(400);

        group.MapPost("/login", async (LoginCommand command, IMediator mediator) =>
        {
            var response = await mediator.Send(command);
            return Results.Ok(response);
        })
        .WithName("LoginUser")
        .AllowAnonymous()
        .Produces<AuthResponse>(200)
        .Produces(400)
        .Produces(401);

        group.MapPost("/forgot-password", async (ForgotPasswordCommand command, IMediator mediator) =>
        {
            var response = await mediator.Send(command);
            return Results.Ok(response);
        })
        .WithName("ForgotPassword")
        .AllowAnonymous()
        .Produces<ForgotPasswordResponse>(200)
        .Produces(400);

        group.MapPost("/reset-password", async (ResetPasswordCommand command, IMediator mediator) =>
        {
            var response = await mediator.Send(command);
            if (!response.Success)
            {
                return Results.BadRequest(response);
            }
            return Results.Ok(response);
        })
        .WithName("ResetPassword")
        .AllowAnonymous()
        .Produces<ResetPasswordResponse>(200)
        .Produces<ResetPasswordResponse>(400);
    }
}

