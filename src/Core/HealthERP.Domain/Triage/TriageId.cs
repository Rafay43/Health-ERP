namespace HealthERP.Domain.Triage;

public readonly record struct TriageId(Guid Value)
{
    public static TriageId New() => new(Guid.NewGuid());
    public static TriageId Empty => new(Guid.Empty);
}
