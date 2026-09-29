using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Appointments.Models;

namespace HealthERP.Application.Appointments.Queries.GetAppointments;

public record GetAppointmentsQuery : IRequest<List<AppointmentDto>>;

public class GetAppointmentsQueryHandler : IRequestHandler<GetAppointmentsQuery, List<AppointmentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAppointmentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AppointmentDto>> Handle(GetAppointmentsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Staff)
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.StartDateTimeUtc)
            .Select(a => new AppointmentDto(
                a.Id.Value,
                a.PatientId.Value,
                a.Patient != null ? $"{a.Patient.FirstName} {a.Patient.LastName}" : "Unknown Patient",
                a.StaffId.Value,
                a.Staff != null ? $"Dr. {a.Staff.FirstName} {a.Staff.LastName}" : "Unknown Doctor",
                a.StartDateTimeUtc,
                a.EndDateTimeUtc,
                a.Reason,
                a.Notes,
                a.Status.ToString()))
            .ToListAsync(cancellationToken);
    }
}
