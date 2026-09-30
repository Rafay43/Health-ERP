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
    string? LinkUrl = null,
    Guid? TargetUserId = null,
    string TargetRole = "All"
) : IRequest<NotificationDto>;

public class SendNotificationCommandHandler : IRequestHandler<SendNotificationCommand, NotificationDto>
{
    private readonly IApplicationDbContext _context;

    public SendNotificationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationDto> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        var notification = Notification.Create(
            request.Title,
            request.Message,
            request.Type,
            request.Severity,
            request.LinkUrl,
            request.TargetUserId,
            request.TargetRole
        );

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return new NotificationDto(
            notification.Id.Value,
            notification.TargetUserId,
            notification.TargetRole,
            notification.Title,
            notification.Message,
            notification.LinkUrl,
            notification.Type.ToString(),
            (int)notification.Type,
            notification.Severity.ToString(),
            (int)notification.Severity,
            notification.IsRead,
            notification.ReadAtUtc,
            notification.CreatedAtUtc
        );
    }
}
