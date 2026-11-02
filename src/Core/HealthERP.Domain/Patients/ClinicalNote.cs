using HealthERP.SharedKernel;
using HealthERP.Domain.StaffMembers;
using StaffEntity = HealthERP.Domain.StaffMembers.StaffMembers;

namespace HealthERP.Domain.Patients;

public class ClinicalNote : BaseEntity<ClinicalNoteId>, IAuditable
{
    public PatientId PatientId { get; private set; }
    public string DoctorName { get; private set; } = default!;
    public StaffId? RecordedByStaffId { get; private set; }
    public StaffEntity? RecordedByStaff { get; private set; }
    public string ChiefComplaint { get; private set; } = default!;
    public string Diagnosis { get; private set; } = default!;
    public string Notes { get; private set; } = default!;
    
    // Patient Vitals Snapshot
    public string? BloodPressure { get; private set; } // e.g. "120/80"
    public int? HeartRateBpm { get; private set; }
    public decimal? TemperatureCelsius { get; private set; }
    public decimal? WeightKg { get; private set; }
    public DateTime NoteDateUtc { get; private set; }

    public Patient? Patient { get; private set; }

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private ClinicalNote() { } // EF Core

    private ClinicalNote(
        ClinicalNoteId id,
        PatientId patientId,
        string doctorName,
        string chiefComplaint,
        string diagnosis,
        string notes,
        string? bloodPressure,
        int? heartRateBpm,
        decimal? temperatureCelsius,
        decimal? weightKg,
        DateTime? recordedAtUtc = null)
    {
        Id = id;
        PatientId = patientId;
        DoctorName = doctorName;
        ChiefComplaint = chiefComplaint;
        Diagnosis = diagnosis;
        Notes = notes;
        BloodPressure = bloodPressure;
        HeartRateBpm = heartRateBpm;
        TemperatureCelsius = temperatureCelsius;
        WeightKg = weightKg;
        NoteDateUtc = recordedAtUtc ?? DateTime.UtcNow;
    }

    public static ClinicalNote Create(
        PatientId patientId,
        string doctorName,
        string chiefComplaint,
        string diagnosis,
        string notes,
        string? bloodPressure = null,
        int? heartRateBpm = null,
        decimal? temperatureCelsius = null,
        decimal? weightKg = null)
    {
        if (patientId.Value == Guid.Empty)
            throw new ArgumentException("Patient is required.", nameof(patientId));
        if (string.IsNullOrWhiteSpace(doctorName))
            throw new ArgumentException("Doctor name is required.", nameof(doctorName));
        if (string.IsNullOrWhiteSpace(diagnosis))
            throw new ArgumentException("Diagnosis is required.", nameof(diagnosis));

        return new ClinicalNote(
            ClinicalNoteId.New(),
            patientId,
            doctorName.Trim(),
            chiefComplaint?.Trim() ?? string.Empty,
            diagnosis.Trim(),
            notes?.Trim() ?? string.Empty,
            bloodPressure?.Trim(),
            heartRateBpm,
            temperatureCelsius,
            weightKg);
    }

    public static ClinicalNote RecordByDoctor(PatientId patientId, StaffId staffId, string staffDisplayName, DateTime recordedAtUtc,
        string? chiefComplaint, string? diagnosis, string? notes, string? bloodPressure, int? heartRateBpm, decimal? temperatureCelsius, decimal? weightKg)
    {
        if (patientId.Value == Guid.Empty || staffId.Value == Guid.Empty) throw new ArgumentException("Patient and recorded Staff are required.");
        if (recordedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Recorded time must be UTC.");
        if (string.IsNullOrWhiteSpace(staffDisplayName)) throw new ArgumentException("Recorded Staff display name is required.");
        var errors = ClinicalNoteRecordingRules.Errors(chiefComplaint, diagnosis, notes, bloodPressure, heartRateBpm, temperatureCelsius, weightKg);
        if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors));
        // DoctorName remains a compatibility display snapshot. The FK is the clinical author identity.
        var display = staffDisplayName.Trim();
        return new ClinicalNote(ClinicalNoteId.New(), patientId, display[..Math.Min(display.Length, 200)],
            chiefComplaint?.Trim() ?? "", diagnosis?.Trim() ?? "", notes?.Trim() ?? "",
            string.IsNullOrWhiteSpace(bloodPressure) ? null : bloodPressure.Trim(), heartRateBpm, temperatureCelsius, weightKg, recordedAtUtc)
        { RecordedByStaffId = staffId };
    }
}
