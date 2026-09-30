using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Triage.Models;
using HealthERP.Domain.Triage;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Triage.Queries;

public record GetEmergencyQueueQuery(bool ActiveOnly = true) : IRequest<List<TriageRecordDto>>;

public class GetEmergencyQueueQueryHandler : IRequestHandler<GetEmergencyQueueQuery, List<TriageRecordDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEmergencyQueueQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TriageRecordDto>> Handle(GetEmergencyQueueQuery request, CancellationToken cancellationToken)
    {
        var query = _context.TriageRecords
            .AsNoTracking()
            .Include(t => t.Patient)
            .AsQueryable();

        if (request.ActiveOnly)
        {
            query = query.Where(t => t.Status != TriageStatus.DischargedFromED && t.Status != TriageStatus.TransferredToInpatient);
        }

        var records = await query
            .OrderBy(t => t.Severity) // Level 1 (Resuscitation) first!
            .ThenBy(t => t.ArrivalDateUtc)
            .ToListAsync(cancellationToken);

        return records.Select(t => new TriageRecordDto(
            t.Id.Value,
            t.PatientId.Value,
            t.Patient != null ? $"{t.Patient.FirstName} {t.Patient.LastName}" : "Unknown Patient",
            t.Patient?.MedicalRecordNumber ?? string.Empty,
            t.ChiefComplaint,
            t.BloodPressure,
            t.HeartRateBpm,
            t.OxygenSaturationSpO2,
            t.BodyTemperatureCelsius,
            t.RespiratoryRateBpm,
            t.Severity.ToString(),
            (int)t.Severity,
            t.Status.ToString(),
            (int)t.Status,
            t.AssignedBay,
            t.TriageNurseName,
            t.AttendingPhysicianName,
            t.ArrivalDateUtc,
            t.DispositionDateUtc,
            t.TransferredBedId?.Value
        )).ToList();
    }
}
