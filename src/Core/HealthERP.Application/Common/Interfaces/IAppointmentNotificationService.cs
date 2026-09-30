namespace HealthERP.Application.Common.Interfaces;

public interface IAppointmentNotificationService
{
    Task NotifyAppointmentChangedAsync(CancellationToken cancellationToken = default);
}
