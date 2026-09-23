using HealthERP.SharedKernel;
using HealthERP.Domain.Patients.Events;

namespace HealthERP.Domain.Patients;

public class Patient : BaseEntity<PatientId>, IAuditable
{
    public string MedicalRecordNumber { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public DateOnly DateOfBirth { get; private set; }
    public string? Gender { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public bool IsDeleted { get; private set; }

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private Patient() { } // EF Core

    private Patient(
        PatientId id,
        string medicalRecordNumber,
        string firstName,
        string lastName,
        DateOnly dateOfBirth)
    {
        Id = id;
        MedicalRecordNumber = medicalRecordNumber;
        FirstName = firstName;
        LastName = lastName;
        DateOfBirth = dateOfBirth;
    }

    public static Patient Register(
        string medicalRecordNumber,
        string firstName,
        string lastName,
        DateOnly dateOfBirth)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Date of birth cannot be in the future.", nameof(dateOfBirth));

        var patient = new Patient(PatientId.New(), medicalRecordNumber, firstName, lastName, dateOfBirth);

        patient.AddDomainEvent(new PatientRegisteredEvent(patient.Id, patient.MedicalRecordNumber));

        return patient;
    }

    public void UpdateContactInfo(string? phoneNumber, string? email)
    {
        PhoneNumber = phoneNumber;
        Email = email;
    }
}