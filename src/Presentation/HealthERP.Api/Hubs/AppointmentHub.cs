using Microsoft.AspNetCore.SignalR;

namespace HealthERP.Api.Hubs;

public class AppointmentHub : Hub
{
    public const string HubUrl = "/hubs/appointments";
}
