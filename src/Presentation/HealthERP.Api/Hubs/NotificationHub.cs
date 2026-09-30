using Microsoft.AspNetCore.SignalR;

namespace HealthERP.Api.Hubs;

public class NotificationHub : Hub
{
    public const string HubUrl = "/hubs/notifications";
}
