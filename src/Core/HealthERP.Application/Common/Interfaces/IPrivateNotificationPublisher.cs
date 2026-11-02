using HealthERP.Domain.Notifications;

namespace HealthERP.Application.Common.Interfaces;

public interface IPrivateNotificationPublisher
{
    Task PublishAsync(Notification notification, CancellationToken cancellationToken = default);
}
