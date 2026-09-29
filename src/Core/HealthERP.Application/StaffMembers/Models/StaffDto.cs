namespace HealthERP.Application.StaffMembers.Models;

public record StaffDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Specialty,
    string LicenseNumber,
    string Department,
    bool IsActive
);
