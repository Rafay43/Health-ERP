using HealthERP.Domain.Patients;
using HealthERP.SharedKernel;

namespace HealthERP.Domain.Pharmacy;

public class Prescription : BaseEntity<PrescriptionId>, IAuditable
{
    public PatientId PatientId { get; private set; }
    public DrugId? DrugId { get; private set; }
    public string DoctorName { get; private set; } = default!;
    public string MedicationName { get; private set; } = default!;
    public string Dosage { get; private set; } = default!;
    public string Frequency { get; private set; } = default!;
    public int DurationDays { get; private set; }
    public int Refills { get; private set; }
    public string? Instructions { get; private set; }
    public PrescriptionStatus Status { get; private set; } = PrescriptionStatus.Pending;
    public DateTime PrescribedDateUtc { get; private set; }
    public DateTime? DispensedDateUtc { get; private set; }

    // Navigation properties
    public Patient? Patient { get; private set; }
    public Drug? Drug { get; private set; }

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private Prescription() { } // EF Core

    private Prescription(
        PrescriptionId id,
        PatientId patientId,
        DrugId? drugId,
        string doctorName,
        string medicationName,
        string dosage,
        string frequency,
        int durationDays,
        int refills,
        string? instructions)
    {
        Id = id;
        PatientId = patientId;
        DrugId = drugId;
        DoctorName = doctorName;
        MedicationName = medicationName;
        Dosage = dosage;
        Frequency = frequency;
        DurationDays = durationDays;
        Refills = refills;
        Instructions = instructions;
        Status = PrescriptionStatus.Pending;
        PrescribedDateUtc = DateTime.UtcNow;
    }

    public static Prescription Create(
        PatientId patientId,
        DrugId? drugId,
        string doctorName,
        string medicationName,
        string dosage,
        string frequency,
        int durationDays,
        int refills,
        string? instructions)
    {
        if (patientId.Value == Guid.Empty)
            throw new ArgumentException("Patient is required.", nameof(patientId));
        if (string.IsNullOrWhiteSpace(doctorName))
            throw new ArgumentException("Doctor name is required.", nameof(doctorName));
        if (string.IsNullOrWhiteSpace(medicationName))
            throw new ArgumentException("Medication name is required.", nameof(medicationName));

        return new Prescription(
            PrescriptionId.New(),
            patientId,
            drugId,
            doctorName.Trim(),
            medicationName.Trim(),
            dosage?.Trim() ?? string.Empty,
            frequency?.Trim() ?? string.Empty,
            durationDays,
            refills,
            instructions?.Trim());
    }

    public void Dispense()
    {
        if (Status == PrescriptionStatus.Dispensed)
            throw new InvalidOperationException("Prescription has already been dispensed.");
        if (Status == PrescriptionStatus.Cancelled)
            throw new InvalidOperationException("Cannot dispense a cancelled prescription.");

        Status = PrescriptionStatus.Dispensed;
        DispensedDateUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == PrescriptionStatus.Dispensed)
            throw new InvalidOperationException("Cannot cancel a dispensed prescription.");

        Status = PrescriptionStatus.Cancelled;
    }
}
