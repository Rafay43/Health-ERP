namespace HealthERP.Domain.Patients;

public readonly record struct ClinicalNoteId(Guid Value)
{
    public static ClinicalNoteId New() => new(Guid.NewGuid());
    public static ClinicalNoteId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
