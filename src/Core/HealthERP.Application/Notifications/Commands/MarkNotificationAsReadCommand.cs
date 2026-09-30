using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Notifications.Commands;

public record MarkNotificationAsReadCommand(Guid NotificationId) : IRequest<bool>;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public MarkNotificationAsReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notificationId = new NotificationId(request.NotificationId);
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        if (notification == null) return false;

        notification.MarkAsRead();
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record MarkAllNotificationsAsReadCommand(Guid? UserId = null) : IRequest<bool>;

public class MarkAllNotificationsAsReadCommandHandler : IRequestHandler<MarkAllNotificationsAsReadCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public MarkAllNotificationsAsReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        var unread = await _context.Notifications
            .Where(n => !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var n in unread)
        {
            n.MarkAsRead();
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
