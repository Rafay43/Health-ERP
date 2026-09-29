namespace HealthERP.Application.Appointments.Models;

public record AppointmentDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid StaffId,
    string DoctorName,
    DateTime StartDateTimeUtc,
    DateTime EndDateTimeUtc,
    string Reason,
    string? Notes,
    string Status
);
