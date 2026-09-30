using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Inpatient.Models;
using HealthERP.Domain.Inpatient;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Inpatient.Commands;

public record DischargePatientCommand(
    Guid AdmissionId,
    string DischargeSummary
) : IRequest<AdmissionDto>;

public class DischargePatientCommandHandler : IRequestHandler<DischargePatientCommand, AdmissionDto>
{
    private readonly IApplicationDbContext _context;

    public DischargePatientCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdmissionDto> Handle(DischargePatientCommand request, CancellationToken cancellationToken)
    {
        var admissionId = new AdmissionId(request.AdmissionId);

        var admission = await _context.Admissions
            .Include(a => a.Patient)
            .Include(a => a.Bed)
            .FirstOrDefaultAsync(a => a.Id == admissionId, cancellationToken);

        if (admission == null)
        {
            throw new KeyNotFoundException($"Admission record with ID '{request.AdmissionId}' was not found.");
        }

        admission.Discharge(request.DischargeSummary);

        if (admission.Bed != null)
        {
            admission.Bed.Release();
        }
        else
        {
            var bed = await _context.Beds.FirstOrDefaultAsync(b => b.Id == admission.BedId, cancellationToken);
            bed?.Release();
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new AdmissionDto(
            admission.Id.Value,
            admission.PatientId.Value,
            admission.Patient != null ? $"{admission.Patient.FirstName} {admission.Patient.LastName}" : "Unknown Patient",
            admission.Patient?.MedicalRecordNumber ?? string.Empty,
            admission.BedId.Value,
            admission.Bed?.WardName ?? string.Empty,
            admission.Bed?.RoomNumber ?? string.Empty,
            admission.Bed?.BedNumber ?? string.Empty,
            admission.AdmittingDoctorName,
            admission.ChiefComplaint,
            admission.AdmissionDateUtc,
            admission.DischargeDateUtc,
            admission.DischargeSummary,
            admission.DailyRateSnapshot,
            admission.TotalRoomCharge,
            admission.IsDischarged
        );
    }
}
