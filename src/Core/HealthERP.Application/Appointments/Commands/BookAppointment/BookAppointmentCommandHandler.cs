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

    public BookAppointmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(BookAppointmentCommand request, CancellationToken cancellationToken)
    {
        var staffId = new StaffId(request.StaffId);
        var patientId = new PatientId(request.PatientId);

        // 1. Conflict Detection Invariant: Check if Doctor is already booked for an overlapping time slot
        var hasConflict = await _context.Appointments
            .AnyAsync(a => a.StaffId == staffId &&
                           a.Status == AppointmentStatus.Scheduled &&
                           !a.IsDeleted &&
                           request.StartDateTimeUtc < a.EndDateTimeUtc &&
                           request.EndDateTimeUtc > a.StartDateTimeUtc, cancellationToken);

        if (hasConflict)
        {
            throw new InvalidOperationException("The selected doctor already has an appointment scheduled during this time slot.");
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
        await _context.SaveChangesAsync(cancellationToken);

        return appointment.Id.Value;
    }
}
