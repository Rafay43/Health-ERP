using HealthERP.Application.Pharmacy.Models;

namespace HealthERP.Application.Common.Interfaces;

public interface IPharmacyDispensingService
{
    Task<PrescriptionDto> DispenseAsync(Guid prescriptionId, int quantity, CancellationToken cancellationToken = default);
}
