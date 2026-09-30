namespace HealthERP.Application.Notifications.Models;

public record NotificationDto(
    Guid Id,
    Guid? TargetUserId,
    string TargetRole,
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
);
