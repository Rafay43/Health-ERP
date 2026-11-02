using HealthERP.Domain.Patients;
using HealthERP.Domain.Appointments;
using HealthERP.SharedKernel;

namespace HealthERP.Domain.Billing;

public class Invoice : BaseEntity<InvoiceId>, IAuditable
{
    public PatientId PatientId { get; private set; }
    public AppointmentId? AppointmentId { get; private set; }
    public string InvoiceNumber { get; private set; } = default!;
    public DateTime IssueDateUtc { get; private set; }
    public DateTime DueDateUtc { get; private set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Pending;
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal BalanceDue => TotalAmount - PaidAmount;

    private readonly List<InvoiceLineItem> _lineItems = new();
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<Payment> _payments = new();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    public Patient? Patient { get; private set; }
    public Appointment? Appointment { get; private set; }

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private Invoice() { } // EF Core

    private Invoice(
        InvoiceId id,
        PatientId patientId,
        string invoiceNumber,
        DateTime dueDateUtc,
        AppointmentId? appointmentId = null)
    {
        Id = id;
        PatientId = patientId;
        InvoiceNumber = invoiceNumber;
        IssueDateUtc = DateTime.UtcNow;
        DueDateUtc = dueDateUtc;
        AppointmentId = appointmentId;
        Status = InvoiceStatus.Pending;
        TotalAmount = 0;
        PaidAmount = 0;
    }

    public static Invoice Create(
        PatientId patientId,
        string invoiceNumber,
        DateTime dueDateUtc,
        AppointmentId? appointmentId = null)
    {
        if (patientId.Value == Guid.Empty)
            throw new ArgumentException("Patient is required.", nameof(patientId));

        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number is required.", nameof(invoiceNumber));

        return new Invoice(
            InvoiceId.New(),
            patientId,
            invoiceNumber,
            dueDateUtc,
            appointmentId);
    }

    public void AddLineItem(string description, int quantity, decimal unitPrice)
    {
        var item = new InvoiceLineItem(Id, description, quantity, unitPrice);
        _lineItems.Add(item);
        RecalculateTotal();
    }

    public Payment RecordPayment(decimal amount, PaymentMethod paymentMethod, string? referenceNumber = null, string? notes = null)
    {
        var payment = new Payment(Id, amount, paymentMethod, referenceNumber, notes);
        if (Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException("A cancelled invoice cannot receive payments.");
        if (Status == InvoiceStatus.Paid || BalanceDue <= 0)
            throw new InvalidOperationException("This invoice is already fully paid.");
        if (amount > BalanceDue)
            throw new InvalidOperationException("Payment amount exceeds balance due. Refresh the invoice before retrying.");

        _payments.Add(payment);

        PaidAmount += amount;

        if (PaidAmount >= TotalAmount)
        {
            Status = InvoiceStatus.Paid;
        }
        else if (PaidAmount > 0)
        {
            Status = InvoiceStatus.PartiallyPaid;
        }

        return payment;
    }

    public void Cancel()
    {
        if (PaidAmount > 0)
            throw new InvalidOperationException("Cannot cancel an invoice with payments applied.");

        Status = InvoiceStatus.Cancelled;
    }

    private void RecalculateTotal()
    {
        TotalAmount = _lineItems.Sum(x => x.LineTotal);
    }
}
