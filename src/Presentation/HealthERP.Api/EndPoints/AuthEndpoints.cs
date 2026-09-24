using MediatR;
using HealthERP.Application.Authentication.Commands.RegisterUser;
using HealthERP.Application.Authentication.Commands.Login;
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
        .Produces<AuthResponse>(200)
        .Produces(400);

        group.MapPost("/login", async (LoginCommand command, IMediator mediator) =>
        {
            var response = await mediator.Send(command);
            return Results.Ok(response);
        })
        .WithName("LoginUser")
        .Produces<AuthResponse>(200)
        .Produces(400);
    }
}
