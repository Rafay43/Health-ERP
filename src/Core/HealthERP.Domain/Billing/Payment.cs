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
        ValidateInput(amount, paymentMethod, referenceNumber, notes);

        Id = Guid.NewGuid();
        InvoiceId = invoiceId;
        PaymentDateUtc = DateTime.UtcNow;
        Amount = amount;
        PaymentMethod = paymentMethod;
        ReferenceNumber = referenceNumber;
        Notes = notes;
    }

    public static void ValidateInput(decimal amount, PaymentMethod paymentMethod, string? referenceNumber, string? notes)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));
        // Reject values PostgreSQL numeric(18,2) would round or cannot represent.
        if (amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Payment amount must fit 18 digits with at most two decimal places.", nameof(amount));
        if (!Enum.IsDefined(paymentMethod))
            throw new ArgumentException("Payment method is invalid.", nameof(paymentMethod));
        if (referenceNumber?.Length > 100)
            throw new ArgumentException("Payment reference must not exceed 100 characters.", nameof(referenceNumber));
        if (notes?.Length > 500)
            throw new ArgumentException("Payment notes must not exceed 500 characters.", nameof(notes));
    }
}
