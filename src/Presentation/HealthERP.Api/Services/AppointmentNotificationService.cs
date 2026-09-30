using Microsoft.AspNetCore.SignalR;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Api.Hubs;

namespace HealthERP.Api.Services;

public class AppointmentNotificationService : IAppointmentNotificationService
{
    private readonly IHubContext<AppointmentHub> _hubContext;

    public AppointmentNotificationService(IHubContext<AppointmentHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyAppointmentChangedAsync(CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("AppointmentUpdated", cancellationToken: cancellationToken);
    }
}
