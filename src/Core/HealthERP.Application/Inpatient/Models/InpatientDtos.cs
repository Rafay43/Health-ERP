namespace HealthERP.Application.Inpatient.Models;

public record BedDto(
    Guid Id,
    string WardName,
    string RoomNumber,
    string BedNumber,
    string BedType,
    decimal DailyRate,
    string Status, // "Available", "Occupied", "Maintenance", "Cleaning"
    int StatusValue,
    Guid? CurrentAdmissionId,
    string? OccupiedByPatientName,
    string? OccupiedByPatientMrn
);

public record AdmissionDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string PatientMrn,
    Guid BedId,
    string WardName,
    string RoomNumber,
    string BedNumber,
    string AdmittingDoctorName,
    string ChiefComplaint,
    DateTime AdmissionDateUtc,
    DateTime? DischargeDateUtc,
    string? DischargeSummary,
    decimal DailyRateSnapshot,
    decimal TotalRoomCharge,
    bool IsDischarged
);
