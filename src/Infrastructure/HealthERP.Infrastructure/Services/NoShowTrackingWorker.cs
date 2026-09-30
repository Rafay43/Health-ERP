using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Appointments;

namespace HealthERP.Infrastructure.Services;

public class NoShowTrackingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NoShowTrackingWorker> _logger;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(5);

    public NoShowTrackingWorker(
        IServiceProvider serviceProvider,
        ILogger<NoShowTrackingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_period);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessPastScheduledAppointmentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing automated no-show appointment tracking.");
            }
        }
    }

    private async Task ProcessPastScheduledAppointmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetService<IAppointmentNotificationService>();

        var now = DateTime.UtcNow;

        var overdueAppointments = await context.Appointments
            .Where(a => a.Status == AppointmentStatus.Scheduled &&
                        !a.IsDeleted &&
                        a.EndDateTimeUtc < now)
            .ToListAsync(cancellationToken);

        if (overdueAppointments.Count == 0) return;

        foreach (var appointment in overdueAppointments)
        {
            appointment.MarkNoShow();
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Automated No-Show Worker marked {Count} overdue appointments as No-Show.", overdueAppointments.Count);

        if (notificationService is not null)
        {
            await notificationService.NotifyAppointmentChangedAsync(cancellationToken);
        }
    }
}
