using HealthERP.Domain.Patients;

namespace HealthERP.Application.Common.Interfaces;

public interface ICurrentPatientService
{
    Task<Patient?> GetCurrentPatientAsync(CancellationToken cancellationToken = default);
    Task<bool> IsCurrentPatientLinkedAsync(CancellationToken cancellationToken = default);
}
