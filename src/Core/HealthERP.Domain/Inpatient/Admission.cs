using HealthERP.Domain.Patients;
using HealthERP.SharedKernel;

namespace HealthERP.Domain.Inpatient;

public class Admission : BaseEntity<AdmissionId>, IAuditable
{
    public PatientId PatientId { get; private set; }
    public BedId BedId { get; private set; }
    public string AdmittingDoctorName { get; private set; } = default!;
    public string ChiefComplaint { get; private set; } = default!;
    public DateTime AdmissionDateUtc { get; private set; }
    public DateTime? DischargeDateUtc { get; private set; }
    public string? DischargeSummary { get; private set; }
    public decimal DailyRateSnapshot { get; private set; }
    public decimal TotalRoomCharge { get; private set; }
    public bool IsDischarged { get; private set; }

    // Navigation properties
    public Patient? Patient { get; private set; }
    public Bed? Bed { get; private set; }

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private Admission() { } // EF Core

    private Admission(
        AdmissionId id,
        PatientId patientId,
        BedId bedId,
        string admittingDoctorName,
        string chiefComplaint,
        decimal dailyRateSnapshot)
    {
        Id = id;
        PatientId = patientId;
        BedId = bedId;
        AdmittingDoctorName = admittingDoctorName;
        ChiefComplaint = chiefComplaint;
        DailyRateSnapshot = dailyRateSnapshot;
        AdmissionDateUtc = DateTime.UtcNow;
        IsDischarged = false;
        TotalRoomCharge = 0;
    }

    public static Admission Create(
        PatientId patientId,
        BedId bedId,
        string admittingDoctorName,
        string chiefComplaint,
        decimal dailyRateSnapshot)
    {
        if (patientId.Value == Guid.Empty)
            throw new ArgumentException("Patient is required.", nameof(patientId));
        if (bedId.Value == Guid.Empty)
            throw new ArgumentException("Bed is required.", nameof(bedId));
        if (string.IsNullOrWhiteSpace(admittingDoctorName))
            throw new ArgumentException("Admitting doctor is required.", nameof(admittingDoctorName));

        return new Admission(
            AdmissionId.New(),
            patientId,
            bedId,
            admittingDoctorName.Trim(),
            chiefComplaint?.Trim() ?? string.Empty,
            dailyRateSnapshot);
    }

    public void Discharge(string dischargeSummary)
    {
        if (IsDischarged)
            throw new InvalidOperationException("Patient has already been discharged.");

        IsDischarged = true;
        DischargeDateUtc = DateTime.UtcNow;
        DischargeSummary = dischargeSummary?.Trim() ?? string.Empty;

        // Calculate Days stayed (minimum 1 day)
        var stayDuration = DischargeDateUtc.Value - AdmissionDateUtc;
        var daysStayed = Math.Max(1, (int)Math.Ceiling(stayDuration.TotalDays));
        TotalRoomCharge = daysStayed * DailyRateSnapshot;
    }

    public void TransferBed(BedId newBedId, decimal newDailyRate, string? transferReason)
    {
        if (IsDischarged)
            throw new InvalidOperationException("Cannot transfer a discharged patient.");

        BedId = newBedId;
        DailyRateSnapshot = newDailyRate;
        if (!string.IsNullOrWhiteSpace(transferReason))
        {
            ChiefComplaint += $" | Bed Transfer Note ({DateTime.UtcNow:yyyy-MM-dd}): {transferReason.Trim()}";
        }
    }
}
