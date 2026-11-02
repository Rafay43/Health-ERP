namespace HealthERP.Application.PatientPortal.Models;

public record PatientHealthRecordDto(
    LatestVitalsSummaryDto? LatestVitals,
    List<PatientEncounterSummaryDto> Encounters);

public record LatestVitalsSummaryDto(
    string? BloodPressure,
    int? HeartRateBpm,
    decimal? TemperatureCelsius,
    decimal? WeightKg,
    DateTime RecordedDateUtc);

public record PatientEncounterSummaryDto(
    Guid Id,
    DateTime NoteDateUtc,
    string DoctorName,
    string ChiefComplaint,
    string Diagnosis);
