using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Patients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Inpatient.Commands;

public record AdmitPatientCommand(
    Guid PatientId,
    Guid BedId,
    string AdmittingDoctorName,
    string ChiefComplaint
) : IRequest<Guid>;

public class AdmitPatientCommandHandler : IRequestHandler<AdmitPatientCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public AdmitPatientCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(AdmitPatientCommand request, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(request.PatientId);
        var bedId = new BedId(request.BedId);

        var patientExists = await _context.Patients.AnyAsync(p => p.Id == patientId, cancellationToken);
        if (!patientExists)
        {
            throw new KeyNotFoundException($"Patient with ID '{request.PatientId}' was not found.");
        }

        var activeAdmission = await _context.Admissions.AnyAsync(a => a.PatientId == patientId && !a.IsDischarged, cancellationToken);
        if (activeAdmission)
        {
            throw new InvalidOperationException("Patient already has an active inpatient admission.");
        }

        var bed = await _context.Beds.FirstOrDefaultAsync(b => b.Id == bedId, cancellationToken);
        if (bed == null)
        {
            throw new KeyNotFoundException($"Bed with ID '{request.BedId}' was not found.");
        }

        // Occupy the bed (validates that status is Available)
        bed.Occupy();

        var admission = Admission.Create(
            patientId,
            bedId,
            request.AdmittingDoctorName,
            request.ChiefComplaint,
            bed.DailyRate
        );

        _context.Admissions.Add(admission);
        await _context.SaveChangesAsync(cancellationToken);

        return admission.Id.Value;
    }
}
