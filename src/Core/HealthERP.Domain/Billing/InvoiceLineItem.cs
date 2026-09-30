using HealthERP.SharedKernel;

namespace HealthERP.Domain.Billing;

public class InvoiceLineItem : BaseEntity<Guid>
{
    public InvoiceId InvoiceId { get; private set; }
    public string Description { get; private set; } = default!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal => Quantity * UnitPrice;

    private InvoiceLineItem() { } // EF Core

    public InvoiceLineItem(InvoiceId invoiceId, string description, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Line item description is required.", nameof(description));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

        Id = Guid.NewGuid();
        InvoiceId = invoiceId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
