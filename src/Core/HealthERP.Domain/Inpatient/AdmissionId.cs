namespace HealthERP.Domain.Inpatient;

public readonly record struct AdmissionId(Guid Value)
{
    public static AdmissionId New() => new(Guid.NewGuid());
    public static AdmissionId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
