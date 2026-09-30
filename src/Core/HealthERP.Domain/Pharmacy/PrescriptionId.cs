namespace HealthERP.Domain.Pharmacy;

public readonly record struct PrescriptionId(Guid Value)
{
    public static PrescriptionId New() => new(Guid.NewGuid());
    public static PrescriptionId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
