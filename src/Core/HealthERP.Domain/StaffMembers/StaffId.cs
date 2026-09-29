namespace HealthERP.Domain.StaffMembers;

public readonly record struct StaffId(Guid Value)
{
    public static StaffId New() => new(Guid.NewGuid());
    public static StaffId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
