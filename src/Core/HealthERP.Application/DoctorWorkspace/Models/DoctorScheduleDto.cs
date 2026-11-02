namespace HealthERP.Application.DoctorWorkspace.Models;

public sealed record DoctorScheduleDto(DateOnly Date, string TimeZoneId, ScheduleDoctorDto Doctor,
    DoctorScheduleSummaryDto Summary, DoctorScheduleAppointmentDto? NextScheduledConsultation,
    IReadOnlyList<DoctorScheduleAppointmentDto> Appointments);

public sealed record ScheduleDoctorDto(string DisplayName, string Specialty, string Department);
public sealed record DoctorScheduleSummaryDto(int Total, int Scheduled, int Confirmed, int Completed, int Cancelled, int NoShow);
public sealed record DoctorScheduleAppointmentDto(Guid AppointmentId, Guid PatientId, string? PatientMedicalRecordNumber,
    string? PatientDisplayName, DateTime StartDateTimeUtc, DateTime EndDateTimeUtc, string Status, string Reason);
