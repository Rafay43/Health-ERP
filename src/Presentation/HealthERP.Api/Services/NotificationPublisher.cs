using HealthERP.Api.Hubs;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Notifications.Models;
using HealthERP.Domain.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace HealthERP.Api.Services;

public class NotificationPublisher(IHubContext<NotificationHub> hubContext) : IPrivateNotificationPublisher
{
    public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        if (notification.TargetUserId is not Guid ownerId || ownerId == Guid.Empty)
            throw new InvalidOperationException("Private notifications require an owner.");

        return hubContext.Clients.User(ownerId.ToString()).SendAsync(
            "ReceiveNotification", NotificationDto.FromEntity(notification), cancellationToken);
    }
}
