namespace HealthERP.Domain.Patients;

public readonly record struct PatientId(Guid Value)
{
    public static PatientId New() => new(Guid.NewGuid());
    public static PatientId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}