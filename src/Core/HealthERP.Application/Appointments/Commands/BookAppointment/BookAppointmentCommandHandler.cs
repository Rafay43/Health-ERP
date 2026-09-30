using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Patients;
using HealthERP.Domain.StaffMembers;

namespace HealthERP.Application.Appointments.Commands.BookAppointment;

public class BookAppointmentCommandHandler : IRequestHandler<BookAppointmentCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IAppointmentNotificationService _notificationService;

    public BookAppointmentCommandHandler(
        IApplicationDbContext context,
        IAppointmentNotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
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

        var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        var patientName = patient != null ? $"{patient.FirstName} {patient.LastName}" : "Patient";

        var notification = HealthERP.Domain.Notifications.Notification.Create(
            "New Appointment Booked",
            $"Appointment scheduled for {patientName} (Reason: {request.Reason}).",
            HealthERP.Domain.Notifications.NotificationType.AppointmentReminder,
            HealthERP.Domain.Notifications.NotificationSeverity.Info,
            "/appointments"
        );
        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyAppointmentChangedAsync(cancellationToken);

        return appointment.Id.Value;
    }
}
