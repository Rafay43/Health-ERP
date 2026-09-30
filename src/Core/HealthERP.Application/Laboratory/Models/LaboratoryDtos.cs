namespace HealthERP.Application.Laboratory.Models;

public record LabTestDto(
    Guid Id,
    string Name,
    string Code,
    string Category,
    string ReferenceRange,
    string Unit,
    decimal StandardPrice,
    string SampleType,
    bool IsActive
);

public record LabOrderDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid? LabTestId,
    string TestName,
    string TestCode,
    string OrderingDoctorName,
    string Priority,
    string Status,
    DateTime OrderedDateUtc,
    string? ResultValue,
    string? ReferenceRange,
    string? Unit,
    string? Remarks,
    string? PerformedBy,
    DateTime? CompletedDateUtc,
    bool IsAbnormal
);
