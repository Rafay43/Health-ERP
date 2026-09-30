using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Patients;
using HealthERP.SharedKernel;

namespace HealthERP.Domain.Triage;

public class TriageRecord : BaseEntity<TriageId>, IAuditable
{
    public PatientId PatientId { get; private set; }
    public string ChiefComplaint { get; private set; } = default!;
    
    // Vital Signs
    public string BloodPressure { get; private set; } = default!; // e.g. 120/80
    public int HeartRateBpm { get; private set; }               // e.g. 78
    public int OxygenSaturationSpO2 { get; private set; }        // e.g. 98%
    public decimal BodyTemperatureCelsius { get; private set; }   // e.g. 37.2
    public int RespiratoryRateBpm { get; private set; }          // e.g. 16

    public TriageSeverity Severity { get; private set; }
    public TriageStatus Status { get; private set; }

    public string? AssignedBay { get; private set; }            // e.g. Trauma Bay 01, ER Bed 3
    public string TriageNurseName { get; private set; } = default!;
    public string? AttendingPhysicianName { get; private set; }

    public DateTime ArrivalDateUtc { get; private set; }
    public DateTime? DispositionDateUtc { get; private set; }

    public BedId? TransferredBedId { get; private set; }

    // Navigation property
    public Patient? Patient { get; private set; }

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private TriageRecord() { } // EF Core

    private TriageRecord(
        TriageId id,
        PatientId patientId,
        string chiefComplaint,
        string bloodPressure,
        int heartRateBpm,
        int oxygenSaturationSpO2,
        decimal bodyTemperatureCelsius,
        int respiratoryRateBpm,
        TriageSeverity severity,
        string triageNurseName,
        string? assignedBay)
    {
        Id = id;
        PatientId = patientId;
        ChiefComplaint = chiefComplaint;
        BloodPressure = bloodPressure;
        HeartRateBpm = heartRateBpm;
        OxygenSaturationSpO2 = oxygenSaturationSpO2;
        BodyTemperatureCelsius = bodyTemperatureCelsius;
        RespiratoryRateBpm = respiratoryRateBpm;
        Severity = severity;
        Status = string.IsNullOrWhiteSpace(assignedBay) ? TriageStatus.Triaged : TriageStatus.InBayCare;
        AssignedBay = assignedBay;
        TriageNurseName = triageNurseName;
        ArrivalDateUtc = DateTime.UtcNow;
    }

    public static TriageRecord Create(
        PatientId patientId,
        string chiefComplaint,
        string bloodPressure,
        int heartRateBpm,
        int oxygenSaturationSpO2,
        decimal bodyTemperatureCelsius,
        int respiratoryRateBpm,
        TriageSeverity severity,
        string triageNurseName,
        string? assignedBay = null)
    {
        if (patientId.Value == Guid.Empty)
            throw new ArgumentException("Patient is required.", nameof(patientId));
        if (string.IsNullOrWhiteSpace(chiefComplaint))
            throw new ArgumentException("Chief complaint is required.", nameof(chiefComplaint));
        if (string.IsNullOrWhiteSpace(triageNurseName))
            throw new ArgumentException("Triage nurse name is required.", nameof(triageNurseName));

        return new TriageRecord(
            TriageId.New(),
            patientId,
            chiefComplaint.Trim(),
            bloodPressure?.Trim() ?? "120/80",
            heartRateBpm,
            oxygenSaturationSpO2,
            bodyTemperatureCelsius,
            respiratoryRateBpm,
            severity,
            triageNurseName.Trim(),
            assignedBay?.Trim());
    }

    public void AssignBay(string bayName, string attendingPhysicianName)
    {
        if (Status == TriageStatus.DischargedFromED || Status == TriageStatus.TransferredToInpatient)
            throw new InvalidOperationException("Cannot assign bay to a resolved triage record.");

        AssignedBay = bayName?.Trim();
        AttendingPhysicianName = attendingPhysicianName?.Trim();
        Status = TriageStatus.InBayCare;
    }

    public void TransferToInpatient(BedId bedId)
    {
        if (Status == TriageStatus.DischargedFromED || Status == TriageStatus.TransferredToInpatient)
            throw new InvalidOperationException("Patient has already been transferred or discharged from Emergency.");

        TransferredBedId = bedId;
        Status = TriageStatus.TransferredToInpatient;
        DispositionDateUtc = DateTime.UtcNow;
    }

    public void DischargeFromED()
    {
        if (Status == TriageStatus.DischargedFromED || Status == TriageStatus.TransferredToInpatient)
            throw new InvalidOperationException("Patient has already been discharged or transferred.");

        Status = TriageStatus.DischargedFromED;
        DispositionDateUtc = DateTime.UtcNow;
    }
}
