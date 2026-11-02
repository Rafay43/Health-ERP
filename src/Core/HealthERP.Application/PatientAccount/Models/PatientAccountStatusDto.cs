namespace HealthERP.Application.PatientAccount.Models;

public record PatientAccountStatusDto(
    bool IsLinked,
    LinkedPatientSummaryDto? Patient = null);

public record LinkedPatientSummaryDto(
    string Mrn,
    string DisplayName);
