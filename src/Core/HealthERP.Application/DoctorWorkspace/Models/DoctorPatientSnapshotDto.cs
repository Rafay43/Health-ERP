namespace HealthERP.Application.DoctorWorkspace.Models;

public sealed record DoctorPatientSnapshotDto(SnapshotPatientDto Patient, SnapshotVitalsDto? LatestVitals,
    IReadOnlyList<SnapshotClinicalNoteDto> RecentNotes, IReadOnlyList<SnapshotPrescriptionDto> RecentPrescriptions,
    IReadOnlyList<SnapshotLabOrderDto> RecentLabOrders, SnapshotAdmissionDto? CurrentAdmission);

public sealed record SnapshotPatientDto(Guid PatientId, string MedicalRecordNumber, string DisplayName,
    DateOnly DateOfBirth, string? Gender);
public sealed record SnapshotVitalsDto(string? BloodPressure, int? HeartRateBpm, decimal? TemperatureCelsius,
    decimal? WeightKg, DateTime RecordedAtUtc);
public sealed record SnapshotClinicalNoteDto(Guid NoteId, DateTime RecordedAtUtc, string ChiefComplaint,
    string Diagnosis, string Notes, string? RecordedByDisplayName = null);
public sealed record SnapshotPrescriptionDto(Guid PrescriptionId, DateTime PrescribedDateUtc, string MedicationName,
    string Dosage, string Frequency, int DurationDays, int Refills, string? Instructions, string Status, DateTime? DispensedDateUtc);
public sealed record SnapshotLabOrderDto(Guid LabOrderId, DateTime OrderedDateUtc, string TestName, string TestCode,
    string Priority, string Status, SnapshotLabResultDto? Result);
public sealed record SnapshotLabResultDto(DateTime? CompletedDateUtc, string? Value, string? ReferenceRange,
    string? Unit, string? Remarks, bool IsAbnormal);
public sealed record SnapshotAdmissionDto(Guid AdmissionId, DateTime AdmissionDateUtc, string ChiefComplaint);
