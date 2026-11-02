using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientPortal.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.PatientPortal.Queries.GetMyHealthRecord;

public record GetMyHealthRecordQuery(
    int Page = 1,
    int PageSize = 25) : IRequest<PatientHealthRecordDto>;

public class GetMyHealthRecordQueryHandler : IRequestHandler<GetMyHealthRecordQuery, PatientHealthRecordDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentPatientService _currentPatientService;

    public GetMyHealthRecordQueryHandler(
        IApplicationDbContext context,
        ICurrentPatientService currentPatientService)
    {
        _context = context;
        _currentPatientService = currentPatientService;
    }

    public async Task<PatientHealthRecordDto> Handle(GetMyHealthRecordQuery request, CancellationToken cancellationToken)
    {
        var patient = await _currentPatientService.GetCurrentPatientAsync(cancellationToken);

        if (patient == null)
        {
            throw new UnauthorizedAccessException("Patient portal activation is required to access health record.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        // 1. Fetch Encounters (Clinical notes snapshot, excluding internal free-text notes)
        var encounters = await _context.ClinicalNotes
            .Where(c => c.PatientId == patient.Id)
            .OrderByDescending(c => c.NoteDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new PatientEncounterSummaryDto(
                c.Id.Value,
                c.NoteDateUtc,
                c.DoctorName,
                c.ChiefComplaint,
                c.Diagnosis))
            .ToListAsync(cancellationToken);

        // 2. Fetch Latest Vitals Snapshot
        var latestNoteWithVitals = await _context.ClinicalNotes
            .Where(c => c.PatientId == patient.Id && (c.BloodPressure != null || c.HeartRateBpm != null || c.TemperatureCelsius != null || c.WeightKg != null))
            .OrderByDescending(c => c.NoteDateUtc)
            .FirstOrDefaultAsync(cancellationToken);

        LatestVitalsSummaryDto? latestVitals = null;
        if (latestNoteWithVitals != null)
        {
            latestVitals = new LatestVitalsSummaryDto(
                latestNoteWithVitals.BloodPressure,
                latestNoteWithVitals.HeartRateBpm,
                latestNoteWithVitals.TemperatureCelsius,
                latestNoteWithVitals.WeightKg,
                latestNoteWithVitals.NoteDateUtc);
        }

        return new PatientHealthRecordDto(latestVitals, encounters);
    }
}
