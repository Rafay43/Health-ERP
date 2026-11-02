namespace HealthERP.Application.PatientPortal.Models;

public record PatientPrescriptionDto(
    Guid Id,
    string MedicationName,
    string Dosage,
    string Frequency,
    int DurationDays,
    int Refills,
    string? Instructions,
    string DoctorName,
    string Status,
    DateTime PrescribedDateUtc,
    DateTime? DispensedDateUtc);
