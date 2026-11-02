using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Notifications.Models;
using HealthERP.Domain.Notifications;
using MediatR;

namespace HealthERP.Application.Notifications.Commands;

public record SendNotificationCommand(
    string Title,
    string Message,
    NotificationType Type,
    NotificationSeverity Severity,
    string? LinkUrl = null
) : IRequest<NotificationDto>;

public class SendNotificationCommandHandler : IRequestHandler<SendNotificationCommand, NotificationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPrivateNotificationPublisher _publisher;

    public SendNotificationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IPrivateNotificationPublisher publisher)
    {
        _context = context;
        _currentUser = currentUser;
        _publisher = publisher;
    }

    public async Task<NotificationDto> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        var notification = Notification.Create(
            request.Title,
            request.Message,
            request.Type,
            request.Severity,
            request.LinkUrl,
            _currentUser.GetRequiredId()
        );

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAsync(notification, cancellationToken);

        return NotificationDto.FromEntity(notification);
    }
}
