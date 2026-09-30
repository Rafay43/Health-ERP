using HealthERP.SharedKernel;

namespace HealthERP.Domain.Patients;

public class ClinicalNote : BaseEntity<ClinicalNoteId>, IAuditable
{
    public PatientId PatientId { get; private set; }
    public string DoctorName { get; private set; } = default!;
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
        decimal? weightKg)
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
        NoteDateUtc = DateTime.UtcNow;
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
}
