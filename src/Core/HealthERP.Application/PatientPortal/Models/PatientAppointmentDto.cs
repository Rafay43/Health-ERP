namespace HealthERP.Application.PatientPortal.Models;

public record PatientAppointmentDto(
    Guid Id,
    DateTime StartDateTimeUtc,
    DateTime EndDateTimeUtc,
    string DoctorName,
    string? Specialty,
    string? Department,
    string Reason,
    string Status);
