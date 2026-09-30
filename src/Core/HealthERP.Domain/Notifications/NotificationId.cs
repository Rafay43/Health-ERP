namespace HealthERP.Domain.Notifications;

public readonly record struct NotificationId(Guid Value)
{
    public static NotificationId New() => new(Guid.NewGuid());
    public static NotificationId Empty => new(Guid.Empty);
}
