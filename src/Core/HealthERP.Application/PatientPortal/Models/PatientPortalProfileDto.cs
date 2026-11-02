namespace HealthERP.Application.PatientPortal.Models;

public record PatientPortalProfileDto(
    string Mrn,
    string FirstName,
    string LastName,
    string DisplayName,
    DateOnly DateOfBirth,
    int Age,
    string? Gender,
    string? PhoneNumber,
    string? Email);
