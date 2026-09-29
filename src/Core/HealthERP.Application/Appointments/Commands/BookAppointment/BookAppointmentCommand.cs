using MediatR;

namespace HealthERP.Application.Appointments.Commands.BookAppointment;

public record BookAppointmentCommand(
    Guid PatientId,
    Guid StaffId,
    DateTime StartDateTimeUtc,
    DateTime EndDateTimeUtc,
    string Reason,
    string? Notes
) : IRequest<Guid>;
