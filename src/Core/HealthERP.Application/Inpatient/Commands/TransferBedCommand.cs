using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Inpatient.Models;
using HealthERP.Domain.Inpatient;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Inpatient.Commands;

public record TransferBedCommand(
    Guid AdmissionId,
    Guid NewBedId,
    string? TransferReason
) : IRequest<AdmissionDto>;

public class TransferBedCommandHandler : IRequestHandler<TransferBedCommand, AdmissionDto>
{
    private readonly IApplicationDbContext _context;

    public TransferBedCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdmissionDto> Handle(TransferBedCommand request, CancellationToken cancellationToken)
    {
        var admissionId = new AdmissionId(request.AdmissionId);
        var newBedId = new BedId(request.NewBedId);

        var admission = await _context.Admissions
            .Include(a => a.Patient)
            .Include(a => a.Bed)
            .FirstOrDefaultAsync(a => a.Id == admissionId, cancellationToken);

        if (admission == null)
        {
            throw new KeyNotFoundException($"Admission record with ID '{request.AdmissionId}' was not found.");
        }

        if (admission.IsDischarged)
        {
            throw new InvalidOperationException("Cannot transfer a discharged patient.");
        }

        if (admission.BedId == newBedId)
        {
            throw new InvalidOperationException("Patient is already assigned to this bed.");
        }

        var newBed = await _context.Beds.FirstOrDefaultAsync(b => b.Id == newBedId, cancellationToken);
        if (newBed == null)
        {
            throw new KeyNotFoundException($"Target bed with ID '{request.NewBedId}' was not found.");
        }

        if (newBed.Status == BedStatus.Occupied)
        {
            throw new InvalidOperationException("Target bed is already occupied.");
        }

        // 1. Release previous bed
        if (admission.Bed != null)
        {
            admission.Bed.Release();
        }
        else
        {
            var oldBed = await _context.Beds.FirstOrDefaultAsync(b => b.Id == admission.BedId, cancellationToken);
            oldBed?.Release();
        }

        // 2. Occupy new bed
        newBed.Occupy();

        // 3. Update admission record to point to new bed & update daily rate snapshot
        admission.TransferBed(newBedId, newBed.DailyRate, request.TransferReason);

        await _context.SaveChangesAsync(cancellationToken);

        return new AdmissionDto(
            admission.Id.Value,
            admission.PatientId.Value,
            admission.Patient != null ? $"{admission.Patient.FirstName} {admission.Patient.LastName}" : "Unknown Patient",
            admission.Patient?.MedicalRecordNumber ?? string.Empty,
            admission.BedId.Value,
            newBed.WardName,
            newBed.RoomNumber,
            newBed.BedNumber,
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
