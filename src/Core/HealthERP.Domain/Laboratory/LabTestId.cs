namespace HealthERP.Domain.Laboratory;

public readonly record struct LabTestId(Guid Value)
{
    public static LabTestId New() => new(Guid.NewGuid());
    public static LabTestId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
