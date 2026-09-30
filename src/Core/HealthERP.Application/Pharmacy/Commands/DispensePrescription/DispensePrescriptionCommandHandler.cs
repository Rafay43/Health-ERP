using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Pharmacy.Models;
using HealthERP.Domain.Pharmacy;

namespace HealthERP.Application.Pharmacy.Commands.DispensePrescription;

public class DispensePrescriptionCommandHandler : IRequestHandler<DispensePrescriptionCommand, PrescriptionDto>
{
    private readonly IApplicationDbContext _context;

    public DispensePrescriptionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PrescriptionDto> Handle(DispensePrescriptionCommand request, CancellationToken cancellationToken)
    {
        var pId = new PrescriptionId(request.PrescriptionId);
        var prescription = await _context.Prescriptions
            .Include(p => p.Patient)
            .Include(p => p.Drug)
            .FirstOrDefaultAsync(p => p.Id == pId, cancellationToken);

        if (prescription == null)
            throw new KeyNotFoundException($"Prescription with ID '{request.PrescriptionId}' was not found.");

        prescription.Dispense();

        if (prescription.Drug != null)
        {
            var qty = request.QuantityToDeduct > 0 ? request.QuantityToDeduct : 1;
            if (prescription.Drug.StockQuantity >= qty)
            {
                prescription.Drug.DeductStock(qty);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new PrescriptionDto(
            prescription.Id.Value,
            prescription.PatientId.Value,
            prescription.Patient != null ? $"{prescription.Patient.FirstName} {prescription.Patient.LastName}" : "Unknown Patient",
            prescription.DrugId?.Value,
            prescription.Drug != null ? prescription.Drug.Name : prescription.MedicationName,
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
