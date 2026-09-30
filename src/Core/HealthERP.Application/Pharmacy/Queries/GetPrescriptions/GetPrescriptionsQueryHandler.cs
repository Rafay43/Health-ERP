using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Pharmacy.Models;
using HealthERP.Domain.Patients;

namespace HealthERP.Application.Pharmacy.Queries.GetPrescriptions;

public class GetPrescriptionsQueryHandler : IRequestHandler<GetPrescriptionsQuery, List<PrescriptionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPrescriptionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PrescriptionDto>> Handle(GetPrescriptionsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Prescriptions
            .Include(p => p.Patient)
            .Include(p => p.Drug)
            .AsNoTracking();

        if (request.PatientId.HasValue)
        {
            var pId = new PatientId(request.PatientId.Value);
            query = query.Where(p => p.PatientId == pId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var statusStr = request.Status.Trim().ToLower();
            query = query.Where(p => p.Status.ToString().ToLower() == statusStr);
        }

        var prescriptions = await query
            .OrderByDescending(p => p.PrescribedDateUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return prescriptions.Select(p => new PrescriptionDto(
            p.Id.Value,
            p.PatientId.Value,
            p.Patient != null ? $"{p.Patient.FirstName} {p.Patient.LastName}" : "Unknown Patient",
            p.DrugId?.Value,
            p.Drug != null ? p.Drug.Name : p.MedicationName,
            p.DoctorName,
            p.MedicationName,
            p.Dosage,
            p.Frequency,
            p.DurationDays,
            p.Refills,
            p.Instructions,
            p.Status.ToString(),
            p.PrescribedDateUtc,
            p.DispensedDateUtc
        )).ToList();
    }
}
