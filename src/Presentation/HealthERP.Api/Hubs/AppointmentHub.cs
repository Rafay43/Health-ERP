using Microsoft.AspNetCore.SignalR;

namespace HealthERP.Api.Hubs;

public class AppointmentHub : Hub
{
    public const string HubUrl = "/hubs/appointments";
    public const string ViewersGroup = "appointment-viewers";

    public override async Task OnConnectedAsync()
    {
        // Membership is server-controlled; the endpoint requires CanViewPatients.
        await Groups.AddToGroupAsync(Context.ConnectionId, ViewersGroup);
        await base.OnConnectedAsync();
    }
}
