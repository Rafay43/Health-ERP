namespace HealthERP.Domain.Billing;

public readonly record struct InvoiceId(Guid Value)
{
    public static InvoiceId New() => new(Guid.NewGuid());
    public static InvoiceId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
