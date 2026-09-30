using HealthERP.SharedKernel;

namespace HealthERP.Domain.Billing;

public class Payment : BaseEntity<Guid>
{
    public InvoiceId InvoiceId { get; private set; }
    public DateTime PaymentDateUtc { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Notes { get; private set; }

    private Payment() { } // EF Core

    public Payment(
        InvoiceId invoiceId,
        decimal amount,
        PaymentMethod paymentMethod,
        string? referenceNumber = null,
        string? notes = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        Id = Guid.NewGuid();
        InvoiceId = invoiceId;
        PaymentDateUtc = DateTime.UtcNow;
        Amount = amount;
        PaymentMethod = paymentMethod;
        ReferenceNumber = referenceNumber;
        Notes = notes;
    }
}
