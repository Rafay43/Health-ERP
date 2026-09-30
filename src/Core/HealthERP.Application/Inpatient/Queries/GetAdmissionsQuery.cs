using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Inpatient.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Inpatient.Queries;

public record GetAdmissionsQuery(Guid? PatientId = null, bool? ActiveOnly = null) : IRequest<List<AdmissionDto>>;

public class GetAdmissionsQueryHandler : IRequestHandler<GetAdmissionsQuery, List<AdmissionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdmissionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AdmissionDto>> Handle(GetAdmissionsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Admissions
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Bed)
            .AsQueryable();

        if (request.PatientId.HasValue && request.PatientId.Value != Guid.Empty)
        {
            query = query.Where(a => a.PatientId == new HealthERP.Domain.Patients.PatientId(request.PatientId.Value));
        }

        if (request.ActiveOnly.HasValue && request.ActiveOnly.Value)
        {
            query = query.Where(a => !a.IsDischarged);
        }

        var admissions = await query
            .OrderByDescending(a => a.AdmissionDateUtc)
            .ToListAsync(cancellationToken);

        return admissions.Select(a => new AdmissionDto(
            a.Id.Value,
            a.PatientId.Value,
            a.Patient != null ? $"{a.Patient.FirstName} {a.Patient.LastName}" : "Unknown Patient",
            a.Patient?.MedicalRecordNumber ?? string.Empty,
            a.BedId.Value,
            a.Bed?.WardName ?? string.Empty,
            a.Bed?.RoomNumber ?? string.Empty,
            a.Bed?.BedNumber ?? string.Empty,
            a.AdmittingDoctorName,
            a.ChiefComplaint,
            a.AdmissionDateUtc,
            a.DischargeDateUtc,
            a.DischargeSummary,
            a.DailyRateSnapshot,
            a.TotalRoomCharge,
            a.IsDischarged
        )).ToList();
    }
}
