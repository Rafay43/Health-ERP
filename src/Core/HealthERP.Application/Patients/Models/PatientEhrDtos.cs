using HealthERP.Application.Billing.Models;
using HealthERP.Application.Pharmacy.Models;
using HealthERP.Application.Laboratory.Models;

namespace HealthERP.Application.Patients.Models;

public record PatientEhrDto(
    Guid PatientId,
    string MedicalRecordNumber,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    int Age,
    string? Gender,
    string? PhoneNumber,
    string? Email,
    VitalsSummaryDto? LatestVitals,
    List<PrescriptionDto> ActiveMedications,
    List<LabOrderDto> PendingLabResults,
    List<EhrTimelineEventDto> Timeline
);

public record VitalsSummaryDto(
    string? BloodPressure,
    int? HeartRateBpm,
    decimal? TemperatureCelsius,
    decimal? WeightKg,
    DateTime RecordedAtUtc
);

public record EhrTimelineEventDto(
    Guid Id,
    string EventType, // Appointment, Prescription, LabOrder, Invoice, ClinicalNote
    DateTime TimestampUtc,
    string Title,
    string Description,
    string Status,
    string BadgeSeverity, // success, info, warn, danger, secondary
    string Icon, // pi-calendar, pi-heart, pi-compass, pi-dollar, pi-file-edit
    object? Details = null
);

public record ClinicalNoteDto(
    Guid Id,
    Guid PatientId,
    string DoctorName,
    string ChiefComplaint,
    string Diagnosis,
    string Notes,
    string? BloodPressure,
    int? HeartRateBpm,
    decimal? TemperatureCelsius,
    decimal? WeightKg,
    DateTime NoteDateUtc
);
