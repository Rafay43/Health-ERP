using HealthERP.Application.Common.Interfaces;

namespace HealthERP.Application.Notifications;

public static class NotificationOwner
{
    public static Guid GetRequiredId(this ICurrentUserService currentUser)
    {
        if (!Guid.TryParse(currentUser.UserId, out var id) || id == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated notification owner is required.");

        return id;
    }
}
