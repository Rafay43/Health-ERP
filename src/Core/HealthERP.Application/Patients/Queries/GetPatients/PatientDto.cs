namespace HealthERP.Application.Patients.Queries.GetPatients;

public record PatientDto(
    Guid Id,
    string MedicalRecordNumber,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth);