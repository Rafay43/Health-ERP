namespace HealthERP.Domain.Inpatient;

public readonly record struct BedId(Guid Value)
{
    public static BedId New() => new(Guid.NewGuid());
    public static BedId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
