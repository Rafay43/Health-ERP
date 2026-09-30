using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Triage;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Triage.Commands;

public record CreateTriageRecordCommand(
    Guid PatientId,
    string ChiefComplaint,
    string BloodPressure,
    int HeartRateBpm,
    int OxygenSaturationSpO2,
    decimal BodyTemperatureCelsius,
    int RespiratoryRateBpm,
    TriageSeverity Severity,
    string TriageNurseName,
    string? AssignedBay = null
) : IRequest<Guid>;

public class CreateTriageRecordCommandHandler : IRequestHandler<CreateTriageRecordCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateTriageRecordCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateTriageRecordCommand request, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(request.PatientId);

        var patientExists = await _context.Patients.AnyAsync(p => p.Id == patientId, cancellationToken);
        if (!patientExists)
        {
            throw new KeyNotFoundException($"Patient with ID '{request.PatientId}' was not found.");
        }

        var triageRecord = TriageRecord.Create(
            patientId,
            request.ChiefComplaint,
            request.BloodPressure,
            request.HeartRateBpm,
            request.OxygenSaturationSpO2,
            request.BodyTemperatureCelsius,
            request.RespiratoryRateBpm,
            request.Severity,
            request.TriageNurseName,
            request.AssignedBay
        );

        _context.TriageRecords.Add(triageRecord);
        await _context.SaveChangesAsync(cancellationToken);

        return triageRecord.Id.Value;
    }
}
