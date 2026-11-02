using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientPortal.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.PatientPortal.Queries.GetMyAppointments;

public record GetMyAppointmentsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null) : IRequest<List<PatientAppointmentDto>>;

public class GetMyAppointmentsQueryHandler : IRequestHandler<GetMyAppointmentsQuery, List<PatientAppointmentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentPatientService _currentPatientService;

    public GetMyAppointmentsQueryHandler(
        IApplicationDbContext context,
        ICurrentPatientService currentPatientService)
    {
        _context = context;
        _currentPatientService = currentPatientService;
    }

    public async Task<List<PatientAppointmentDto>> Handle(GetMyAppointmentsQuery request, CancellationToken cancellationToken)
    {
        var patient = await _currentPatientService.GetCurrentPatientAsync(cancellationToken);

        if (patient == null)
        {
            throw new UnauthorizedAccessException("Patient portal activation is required to access appointments.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.Appointments
            .Include(a => a.Staff)
            .Where(a => a.PatientId == patient.Id && !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var statusStr = request.Status.Trim();
            if (Enum.TryParse<HealthERP.Domain.Appointments.AppointmentStatus>(statusStr, true, out var parsedStatus))
            {
                query = query.Where(a => a.Status == parsedStatus);
            }
        }

        return await query
            .OrderByDescending(a => a.StartDateTimeUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new PatientAppointmentDto(
                a.Id.Value,
                a.StartDateTimeUtc,
                a.EndDateTimeUtc,
                a.Staff != null ? $"Dr. {a.Staff.FirstName} {a.Staff.LastName}".Trim() : "Attending Doctor",
                a.Staff != null ? a.Staff.Specialty : "General Medicine",
                a.Staff != null ? a.Staff.Department : "Outpatient Clinic",
                a.Reason,
                a.Status.ToString()))
            .ToListAsync(cancellationToken);
    }
}
