using HealthERP.Domain.Notifications;
using Xunit;

namespace HealthERP.Domain.UnitTests.Notifications;

public class NotificationDomainTests
{
    [Fact]
    public void Create_ValidParameters_ShouldInitializeUnreadNotification()
    {
        var notification = Notification.Create(
            "Emergency Level 1 Alert",
            "Patient John Doe arrived with severe chest pain.",
            NotificationType.EmergencyAlert,
            NotificationSeverity.Critical,
            "/emergency",
            targetRole: "Doctor");

        Assert.NotNull(notification);
        Assert.Equal("Emergency Level 1 Alert", notification.Title);
        Assert.Equal("Patient John Doe arrived with severe chest pain.", notification.Message);
        Assert.Equal(NotificationType.EmergencyAlert, notification.Type);
        Assert.Equal(NotificationSeverity.Critical, notification.Severity);
        Assert.Equal("/emergency", notification.LinkUrl);
        Assert.Equal("Doctor", notification.TargetRole);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAtUtc);
    }

    [Fact]
    public void MarkAsRead_WhenUnread_ShouldSetIsReadAndTimestamp()
    {
        var notification = Notification.Create(
            "Low Stock Alert",
            "Amoxicillin 500mg stock is below reorder threshold.",
            NotificationType.LowStockWarning,
            NotificationSeverity.Warning,
            "/pharmacy");

        notification.MarkAsRead();

        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAtUtc);
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Notification.Create("", "Message", NotificationType.SystemInfo, NotificationSeverity.Info));
    }
}
