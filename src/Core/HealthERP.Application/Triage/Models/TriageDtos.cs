namespace HealthERP.Application.Triage.Models;

public record TriageRecordDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string PatientMrn,
    string ChiefComplaint,
    string BloodPressure,
    int HeartRateBpm,
    int OxygenSaturationSpO2,
    decimal BodyTemperatureCelsius,
    int RespiratoryRateBpm,
    string SeverityName,
    int SeverityLevel,
    string StatusName,
    int StatusValue,
    string? AssignedBay,
    string TriageNurseName,
    string? AttendingPhysicianName,
    DateTime ArrivalDateUtc,
    DateTime? DispositionDateUtc,
    Guid? TransferredBedId
);
