using HealthERP.Domain.Patients;
using HealthERP.SharedKernel;

namespace HealthERP.Domain.Laboratory;

public class LabOrder : BaseEntity<LabOrderId>, IAuditable
{
    public PatientId PatientId { get; private set; }
    public LabTestId? LabTestId { get; private set; }
    public string OrderingDoctorName { get; private set; } = default!;
    public string TestName { get; private set; } = default!;
    public string TestCode { get; private set; } = default!;
    public string Priority { get; private set; } = "Routine"; // Routine, Urgent, STAT
    public LabOrderStatus Status { get; private set; } = LabOrderStatus.Ordered;
    public DateTime OrderedDateUtc { get; private set; }
    
    // Result details
    public string? ResultValue { get; private set; }
    public string? ReferenceRange { get; private set; }
    public string? Unit { get; private set; }
    public string? Remarks { get; private set; }
    public string? PerformedBy { get; private set; }
    public DateTime? CompletedDateUtc { get; private set; }
    public bool IsAbnormal { get; private set; }

    // Navigation properties
    public Patient? Patient { get; private set; }
    public LabTest? LabTest { get; private set; }

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private LabOrder() { } // EF Core

    private LabOrder(
        LabOrderId id,
        PatientId patientId,
        LabTestId? labTestId,
        string orderingDoctorName,
        string testName,
        string testCode,
        string priority,
        string? referenceRange,
        string? unit)
    {
        Id = id;
        PatientId = patientId;
        LabTestId = labTestId;
        OrderingDoctorName = orderingDoctorName;
        TestName = testName;
        TestCode = testCode;
        Priority = priority;
        ReferenceRange = referenceRange;
        Unit = unit;
        Status = LabOrderStatus.Ordered;
        OrderedDateUtc = DateTime.UtcNow;
    }

    public static LabOrder Create(
        PatientId patientId,
        LabTestId? labTestId,
        string orderingDoctorName,
        string testName,
        string testCode,
        string priority = "Routine",
        string? referenceRange = null,
        string? unit = null)
    {
        if (patientId.Value == Guid.Empty)
            throw new ArgumentException("Patient is required.", nameof(patientId));
        if (string.IsNullOrWhiteSpace(orderingDoctorName))
            throw new ArgumentException("Ordering doctor name is required.", nameof(orderingDoctorName));
        if (string.IsNullOrWhiteSpace(testName))
            throw new ArgumentException("Test name is required.", nameof(testName));

        return new LabOrder(
            LabOrderId.New(),
            patientId,
            labTestId,
            orderingDoctorName.Trim(),
            testName.Trim(),
            testCode?.Trim().ToUpper() ?? "CUSTOM",
            priority?.Trim() ?? "Routine",
            referenceRange?.Trim(),
            unit?.Trim());
    }

    public void MarkInProgress(string? performedBy = null)
    {
        if (Status == LabOrderStatus.Completed)
            throw new InvalidOperationException("Cannot mark a completed lab order as in-progress.");
        if (Status == LabOrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot process a cancelled lab order.");

        Status = LabOrderStatus.InProgress;
        if (!string.IsNullOrWhiteSpace(performedBy))
        {
            PerformedBy = performedBy.Trim();
        }
    }

    public void RecordResult(string resultValue, string? remarks, string performedBy, bool isAbnormal = false)
    {
        if (Status == LabOrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot record result for a cancelled lab order.");
        if (string.IsNullOrWhiteSpace(resultValue))
            throw new ArgumentException("Result value is required.", nameof(resultValue));

        ResultValue = resultValue.Trim();
        Remarks = remarks?.Trim();
        PerformedBy = performedBy?.Trim() ?? "Lab Tech";
        IsAbnormal = isAbnormal;
        Status = LabOrderStatus.Completed;
        CompletedDateUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == LabOrderStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed lab order.");

        Status = LabOrderStatus.Cancelled;
    }
}
