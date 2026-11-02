namespace HealthERP.Application.Notifications.Models;

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    string? LinkUrl,
    string TypeName,
    int TypeValue,
    string SeverityName,
    int SeverityValue,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc
)
{
    public static NotificationDto FromEntity(HealthERP.Domain.Notifications.Notification notification) => new(
        notification.Id.Value, notification.Title, notification.Message, notification.LinkUrl,
        notification.Type.ToString(), (int)notification.Type,
        notification.Severity.ToString(), (int)notification.Severity,
        notification.IsRead, notification.ReadAtUtc, notification.CreatedAtUtc);
}
