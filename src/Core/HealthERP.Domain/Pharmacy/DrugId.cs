namespace HealthERP.Domain.Pharmacy;

public readonly record struct DrugId(Guid Value)
{
    public static DrugId New() => new(Guid.NewGuid());
    public static DrugId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
