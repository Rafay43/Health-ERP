namespace HealthERP.Domain.Laboratory;

public readonly record struct LabOrderId(Guid Value)
{
    public static LabOrderId New() => new(Guid.NewGuid());
    public static LabOrderId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
