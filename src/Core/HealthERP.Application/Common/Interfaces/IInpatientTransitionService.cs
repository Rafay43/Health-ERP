using HealthERP.Application.Inpatient.Models;

namespace HealthERP.Application.Common.Interfaces;

public interface IInpatientTransitionService
{
    Task<Guid> AdmitAsync(Guid patientId, Guid bedId, string doctor, string complaint, CancellationToken cancellationToken = default);
    Task<AdmissionDto> TransferAsync(Guid admissionId, Guid destinationId, string? reason, CancellationToken cancellationToken = default);
    Task<AdmissionDto> DischargeAsync(Guid admissionId, string summary, CancellationToken cancellationToken = default);
    Task<Guid> AdmitFromEmergencyAsync(Guid triageId, Guid bedId, string doctor, string notes, CancellationToken cancellationToken = default);
}
