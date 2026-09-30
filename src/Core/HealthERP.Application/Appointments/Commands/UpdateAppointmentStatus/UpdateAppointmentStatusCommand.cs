using MediatR;
using HealthERP.Domain.Appointments;

namespace HealthERP.Application.Appointments.Commands.UpdateAppointmentStatus;

public record UpdateAppointmentStatusCommand(Guid Id, AppointmentStatus Status) : IRequest;
