using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Notifications.Commands;

public record MarkNotificationAsReadCommand(Guid NotificationId) : IRequest<bool>;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationAsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notificationId = new NotificationId(request.NotificationId);
        var ownerId = _currentUser.GetRequiredId();
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.TargetUserId == ownerId, cancellationToken);
        if (notification == null) return false;

        notification.MarkAsRead();
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record MarkAllNotificationsAsReadCommand() : IRequest<bool>;

public class MarkAllNotificationsAsReadCommandHandler : IRequestHandler<MarkAllNotificationsAsReadCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MarkAllNotificationsAsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        var ownerId = _currentUser.GetRequiredId();
        var unread = await _context.Notifications
            .Where(n => n.TargetUserId == ownerId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var n in unread)
        {
            n.MarkAsRead();
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
