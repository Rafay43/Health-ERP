using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientPortal.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.PatientPortal.Queries.GetMyPrescriptions;

public record GetMyPrescriptionsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null) : IRequest<List<PatientPrescriptionDto>>;

public class GetMyPrescriptionsQueryHandler : IRequestHandler<GetMyPrescriptionsQuery, List<PatientPrescriptionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentPatientService _currentPatientService;

    public GetMyPrescriptionsQueryHandler(
        IApplicationDbContext context,
        ICurrentPatientService currentPatientService)
    {
        _context = context;
        _currentPatientService = currentPatientService;
    }

    public async Task<List<PatientPrescriptionDto>> Handle(GetMyPrescriptionsQuery request, CancellationToken cancellationToken)
    {
        var patient = await _currentPatientService.GetCurrentPatientAsync(cancellationToken);

        if (patient == null)
        {
            throw new UnauthorizedAccessException("Patient portal activation is required to access prescriptions.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.Prescriptions
            .Where(p => p.PatientId == patient.Id);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var statusStr = request.Status.Trim();
            if (Enum.TryParse<HealthERP.Domain.Pharmacy.PrescriptionStatus>(statusStr, true, out var parsedStatus))
            {
                query = query.Where(p => p.Status == parsedStatus);
            }
        }

        return await query
            .OrderByDescending(p => p.PrescribedDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PatientPrescriptionDto(
                p.Id.Value,
                p.MedicationName,
                p.Dosage,
                p.Frequency,
                p.DurationDays,
                p.Refills,
                p.Instructions,
                p.DoctorName,
                p.Status.ToString(),
                p.PrescribedDateUtc,
                p.DispensedDateUtc))
            .ToListAsync(cancellationToken);
    }
}
