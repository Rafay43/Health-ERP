using HealthERP.Domain.Patients;
using HealthERP.Domain.StaffMembers;
using StaffEntity = HealthERP.Domain.StaffMembers.StaffMembers;
using HealthERP.SharedKernel;

namespace HealthERP.Domain.Appointments;

public class Appointment : BaseEntity<AppointmentId>, IAuditable
{
    public PatientId PatientId { get; private set; }
    public StaffId StaffId { get; private set; }
    public DateTime StartDateTimeUtc { get; private set; }
    public DateTime EndDateTimeUtc { get; private set; }
    public string Reason { get; private set; } = default!;
    public string? Notes { get; private set; }
    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Scheduled;
    public bool IsDeleted { get; private set; } = false;

    // Navigation properties for EF Core
    public Patient? Patient { get; private set; }
    public StaffEntity? Staff { get; private set; }

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private Appointment() { } // EF Core

    private Appointment(
        AppointmentId id,
        PatientId patientId,
        StaffId staffId,
        DateTime startDateTimeUtc,
        DateTime endDateTimeUtc,
        string reason,
        string? notes)
    {
        Id = id;
        PatientId = patientId;
        StaffId = staffId;
        StartDateTimeUtc = startDateTimeUtc;
        EndDateTimeUtc = endDateTimeUtc;
        Reason = reason;
        Notes = notes;
        Status = AppointmentStatus.Scheduled;
    }

    public static Appointment Schedule(
        PatientId patientId,
        StaffId staffId,
        DateTime startDateTimeUtc,
        DateTime endDateTimeUtc,
        string reason,
        string? notes = null)
    {
        if (patientId.Value == Guid.Empty)
            throw new ArgumentException("Patient is required.", nameof(patientId));

        if (staffId.Value == Guid.Empty)
            throw new ArgumentException("Doctor/Staff is required.", nameof(staffId));

        if (startDateTimeUtc >= endDateTimeUtc)
            throw new ArgumentException("Start time must be earlier than end time.", nameof(startDateTimeUtc));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason for appointment is required.", nameof(reason));

        return new Appointment(
            AppointmentId.New(),
            patientId,
            staffId,
            startDateTimeUtc,
            endDateTimeUtc,
            reason,
            notes);
    }

    public void Complete() => Status = AppointmentStatus.Completed;
    public void Cancel() => Status = AppointmentStatus.Cancelled;
    public void MarkNoShow() => Status = AppointmentStatus.NoShow;
}
