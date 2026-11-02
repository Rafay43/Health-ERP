using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Patients;
using HealthERP.Domain.StaffMembers;
using HealthERP.Application.Notifications;

namespace HealthERP.Application.Appointments.Commands.BookAppointment;

public class BookAppointmentCommandHandler : IRequestHandler<BookAppointmentCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IAppointmentNotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPrivateNotificationPublisher _publisher;

    public BookAppointmentCommandHandler(
        IApplicationDbContext context,
        IAppointmentNotificationService notificationService,
        ICurrentUserService currentUser,
        IPrivateNotificationPublisher publisher)
    {
        _context = context;
        _notificationService = notificationService;
        _currentUser = currentUser;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(BookAppointmentCommand request, CancellationToken cancellationToken)
    {
        var staffId = new StaffId(request.StaffId);
        var patientId = new PatientId(request.PatientId);

        // 1. Conflict Detection Invariant: Check if Doctor is already booked for an overlapping time slot
        var hasConflict = await _context.Appointments
            .AnyAsync(a => a.StaffId == staffId &&
                           (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed) &&
                           !a.IsDeleted &&
                           request.StartDateTimeUtc < a.EndDateTimeUtc &&
                           request.EndDateTimeUtc > a.StartDateTimeUtc, cancellationToken);

        if (hasConflict)
        {
            throw new InvalidOperationException("Doctor is already booked for a consultation during this time slot.");
        }

        // 2. Schedule Appointment
        var appointment = Appointment.Schedule(
            patientId,
            staffId,
            request.StartDateTimeUtc,
            request.EndDateTimeUtc,
            request.Reason,
            request.Notes);

        _context.Appointments.Add(appointment);

        var notification = HealthERP.Domain.Notifications.Notification.Create(
            "New Appointment Booked",
            "An appointment was scheduled. Open appointments for details.",
            HealthERP.Domain.Notifications.NotificationType.AppointmentReminder,
            HealthERP.Domain.Notifications.NotificationSeverity.Info,
            "/appointments",
            targetUserId: _currentUser.GetRequiredId()
        );
        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAsync(notification, cancellationToken);

        await _notificationService.NotifyAppointmentChangedAsync(cancellationToken);

        return appointment.Id.Value;
    }
}
