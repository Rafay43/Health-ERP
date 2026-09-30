namespace HealthERP.Application.Admin.Models;

public record UserAccountDto(
    Guid Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    List<string> Roles,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record AuditLogEntryDto(
    Guid Id,
    string EntityName, // Patient, Prescription, LabOrder, Invoice, ClinicalNote
    string EntityId,
    string Action, // Created, Modified
    string PerformedBy,
    DateTime TimestampUtc,
    string Details
);

public record SystemHealthDto(
    string Status, // Healthy, Degraded, Unhealthy
    string DatabaseProvider,
    bool DatabaseCanConnect,
    int TotalUsers,
    int ActiveRolesCount,
    DateTime ServerTimeUtc
);
