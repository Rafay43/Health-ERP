using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Notifications.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Notifications.Queries;

public record GetUserNotificationsQuery(bool UnreadOnly = false) : IRequest<List<NotificationDto>>;

public class GetUserNotificationsQueryHandler : IRequestHandler<GetUserNotificationsQuery, List<NotificationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUserNotificationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<NotificationDto>> Handle(GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        var ownerId = _currentUser.GetRequiredId();
        var query = _context.Notifications.AsNoTracking().Where(n => n.TargetUserId == ownerId);

        if (request.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return notifications.Select(NotificationDto.FromEntity).ToList();
    }
}
