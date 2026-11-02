namespace HealthERP.Application.PatientPortal.Models;

public record PatientLabResultDto(
    Guid Id,
    string TestName,
    string TestCode,
    string Priority,
    string OrderingDoctorName,
    DateTime OrderedDateUtc,
    string Status,
    string? ResultValue,
    string? ReferenceRange,
    string? Unit,
    string? PerformedBy,
    DateTime? CompletedDateUtc,
    bool IsAbnormal);
