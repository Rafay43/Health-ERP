using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Triage;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Triage.Commands;

public record TransferToInpatientCommand(
    Guid TriageId,
    Guid BedId,
    string AdmittingDoctorName,
    string AdmissionNotes
) : IRequest<Guid>;

public class TransferToInpatientCommandHandler : IRequestHandler<TransferToInpatientCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public TransferToInpatientCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(TransferToInpatientCommand request, CancellationToken cancellationToken)
    {
        var triageId = new TriageId(request.TriageId);
        var bedId = new BedId(request.BedId);

        var triageRecord = await _context.TriageRecords
            .Include(t => t.Patient)
            .FirstOrDefaultAsync(t => t.Id == triageId, cancellationToken);

        if (triageRecord == null)
        {
            throw new KeyNotFoundException($"Triage record '{request.TriageId}' was not found.");
        }

        var bed = await _context.Beds.FirstOrDefaultAsync(b => b.Id == bedId, cancellationToken);
        if (bed == null)
        {
            throw new KeyNotFoundException($"Inpatient bed '{request.BedId}' was not found.");
        }

        // Occupy inpatient bed
        bed.Occupy();

        // Create Admission
        var admission = Admission.Create(
            triageRecord.PatientId,
            bedId,
            request.AdmittingDoctorName,
            $"[ER Transfer] {request.AdmissionNotes} (Original Complaint: {triageRecord.ChiefComplaint})",
            bed.DailyRate
        );

        _context.Admissions.Add(admission);

        // Update Triage Record
        triageRecord.TransferToInpatient(bedId);

        await _context.SaveChangesAsync(cancellationToken);

        return admission.Id.Value;
    }
}
