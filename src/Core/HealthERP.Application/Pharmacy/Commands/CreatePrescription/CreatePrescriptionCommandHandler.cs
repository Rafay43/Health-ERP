using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Pharmacy.Models;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Pharmacy;

namespace HealthERP.Application.Pharmacy.Commands.CreatePrescription;

public class CreatePrescriptionCommandHandler : IRequestHandler<CreatePrescriptionCommand, PrescriptionDto>
{
    private readonly IApplicationDbContext _context;

    public CreatePrescriptionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PrescriptionDto> Handle(CreatePrescriptionCommand request, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(request.PatientId);
        var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient == null)
            throw new KeyNotFoundException($"Patient with ID '{request.PatientId}' was not found.");

        DrugId? drugId = request.DrugId.HasValue ? new DrugId(request.DrugId.Value) : null;
        string medicationName = request.MedicationName;

        if (drugId.HasValue)
        {
            var drug = await _context.Drugs.FirstOrDefaultAsync(d => d.Id == drugId.Value, cancellationToken);
            if (drug != null)
            {
                medicationName = drug.Name;
            }
        }

        var prescription = Prescription.Create(
            patientId,
            drugId,
            request.DoctorName,
            medicationName,
            request.Dosage,
            request.Frequency,
            request.DurationDays,
            request.Refills,
            request.Instructions);

        _context.Prescriptions.Add(prescription);
        await _context.SaveChangesAsync(cancellationToken);

        return new PrescriptionDto(
            prescription.Id.Value,
            prescription.PatientId.Value,
            $"{patient.FirstName} {patient.LastName}",
            prescription.DrugId?.Value,
            medicationName,
            prescription.DoctorName,
            prescription.MedicationName,
            prescription.Dosage,
            prescription.Frequency,
            prescription.DurationDays,
            prescription.Refills,
            prescription.Instructions,
            prescription.Status.ToString(),
            prescription.PrescribedDateUtc,
            prescription.DispensedDateUtc);
    }
}
