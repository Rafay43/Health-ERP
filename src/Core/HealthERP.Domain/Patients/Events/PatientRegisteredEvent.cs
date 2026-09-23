using HealthERP.SharedKernel;

namespace HealthERP.Domain.Patients.Events;

public record PatientRegisteredEvent(PatientId PatientId, string MedicalRecordNumber) : IDomainEvent;