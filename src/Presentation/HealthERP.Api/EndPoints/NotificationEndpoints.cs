using HealthERP.Api.Hubs;
using HealthERP.Application.Notifications.Commands;
using HealthERP.Application.Notifications.Models;
using HealthERP.Application.Notifications.Queries;
using MediatR;

namespace HealthERP.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .WithTags("Notifications")
            .RequireAuthorization(NotificationSecurity.OwnerPolicy);

        group.MapGet("/", async (IMediator mediator, bool unreadOnly = false) =>
        {
            var result = await mediator.Send(new GetUserNotificationsQuery(UnreadOnly: unreadOnly));
            return Results.Ok(result);
        })
        .WithName("GetUserNotifications")
        .Produces<List<NotificationDto>>(200);

        group.MapPost("/", async (
            SendNotificationCommand command,
            IMediator mediator) =>
        {
            var dto = await mediator.Send(command);

            return Results.Created($"/api/v1/notifications/{dto.Id}", dto);
        })
        .WithName("SendNotification")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanEditPatients)
        .Produces<NotificationDto>(201)
        .Produces(400);

        group.MapPost("/{id:guid}/read", async (Guid id, IMediator mediator) =>
        {
            var success = await mediator.Send(new MarkNotificationAsReadCommand(id));
            return success ? Results.Ok(new { success }) : Results.NotFound();
        })
        .WithName("MarkNotificationAsRead")
        .Produces<object>(200)
        .Produces(404);

        group.MapPost("/read-all", async (IMediator mediator) =>
        {
            var success = await mediator.Send(new MarkAllNotificationsAsReadCommand());
            return Results.Ok(new { success });
        })
        .WithName("MarkAllNotificationsAsRead")
        .Produces<object>(200);
    }
}
