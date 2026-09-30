using HealthERP.Api.Hubs;
using HealthERP.Application.Notifications.Commands;
using HealthERP.Application.Notifications.Models;
using HealthERP.Application.Notifications.Queries;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace HealthERP.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", async (IMediator mediator, bool unreadOnly = false) =>
        {
            var result = await mediator.Send(new GetUserNotificationsQuery(UnreadOnly: unreadOnly));
            return Results.Ok(result);
        })
        .WithName("GetUserNotifications")
        .Produces<List<NotificationDto>>(200);

        group.MapPost("/", async (
            SendNotificationCommand command,
            IMediator mediator,
            IHubContext<NotificationHub> hubContext) =>
        {
            var dto = await mediator.Send(command);

            // Broadcast in real-time via SignalR WebSocket!
            await hubContext.Clients.All.SendAsync("ReceiveNotification", dto);

            return Results.Created($"/api/v1/notifications/{dto.Id}", dto);
        })
        .WithName("SendNotification")
        .Produces<NotificationDto>(201)
        .Produces(400);

        group.MapPost("/{id:guid}/read", async (Guid id, IMediator mediator) =>
        {
            var success = await mediator.Send(new MarkNotificationAsReadCommand(id));
            return Results.Ok(new { success });
        })
        .WithName("MarkNotificationAsRead")
        .Produces<object>(200);

        group.MapPost("/read-all", async (IMediator mediator) =>
        {
            var success = await mediator.Send(new MarkAllNotificationsAsReadCommand());
            return Results.Ok(new { success });
        })
        .WithName("MarkAllNotificationsAsRead")
        .Produces<object>(200);
    }
}
