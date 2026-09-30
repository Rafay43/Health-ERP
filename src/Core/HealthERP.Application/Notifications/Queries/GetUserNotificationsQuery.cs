using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Notifications.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Notifications.Queries;

public record GetUserNotificationsQuery(Guid? UserId = null, string? Role = null, bool UnreadOnly = false) : IRequest<List<NotificationDto>>;

public class GetUserNotificationsQueryHandler : IRequestHandler<GetUserNotificationsQuery, List<NotificationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUserNotificationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<NotificationDto>> Handle(GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Notifications.AsNoTracking().AsQueryable();

        if (request.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
        {
            query = query.Where(n => n.TargetUserId == request.UserId.Value || n.TargetRole == "All" || (request.Role != null && n.TargetRole == request.Role));
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return notifications.Select(n => new NotificationDto(
            n.Id.Value,
            n.TargetUserId,
            n.TargetRole,
            n.Title,
            n.Message,
            n.LinkUrl,
            n.Type.ToString(),
            (int)n.Type,
            n.Severity.ToString(),
            (int)n.Severity,
            n.IsRead,
            n.ReadAtUtc,
            n.CreatedAtUtc
        )).ToList();
    }
}
