using HealthERP.SharedKernel;

namespace HealthERP.Domain.Notifications;

public class Notification : BaseEntity<NotificationId>, IAuditable
{
    public Guid? TargetUserId { get; private set; }
    public string TargetRole { get; private set; } = "All"; // All, Doctor, Nurse, Staff, Admin
    public string Title { get; private set; } = default!;
    public string Message { get; private set; } = default!;
    public string? LinkUrl { get; private set; }

    public NotificationType Type { get; private set; }
    public NotificationSeverity Severity { get; private set; }

    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private Notification() { } // EF Core

    private Notification(
        NotificationId id,
        Guid? targetUserId,
        string targetRole,
        string title,
        string message,
        string? linkUrl,
        NotificationType type,
        NotificationSeverity severity)
    {
        Id = id;
        TargetUserId = targetUserId;
        TargetRole = string.IsNullOrWhiteSpace(targetRole) ? "All" : targetRole.Trim();
        Title = title;
        Message = message;
        LinkUrl = linkUrl?.Trim();
        Type = type;
        Severity = severity;
        IsRead = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Notification Create(
        string title,
        string message,
        NotificationType type,
        NotificationSeverity severity,
        string? linkUrl = null,
        Guid? targetUserId = null,
        string targetRole = "All")
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));

        return new Notification(
            NotificationId.New(),
            targetUserId,
            targetRole,
            title.Trim(),
            message.Trim(),
            linkUrl,
            type,
            severity);
    }

    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAtUtc = DateTime.UtcNow;
        }
    }
}
