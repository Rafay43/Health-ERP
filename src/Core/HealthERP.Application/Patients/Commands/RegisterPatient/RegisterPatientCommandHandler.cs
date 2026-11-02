using MediatR;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Patients;

namespace HealthERP.Application.Patients.Commands.RegisterPatient;

public class RegisterPatientCommandHandler : IRequestHandler<RegisterPatientCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public RegisterPatientCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    private static readonly string[] AllowedGenders = ["Male", "Female", "Other", "Prefer not to say"];

    public async Task<Guid> Handle(RegisterPatientCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Gender))
        {
            throw new ArgumentException("Gender is required for patient registration.", nameof(request.Gender));
        }

        var normalizedGender = AllowedGenders.FirstOrDefault(g => g.Equals(request.Gender.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalizedGender == null)
        {
            throw new ArgumentException($"Invalid gender specified. Allowed values: {string.Join(", ", AllowedGenders)}.", nameof(request.Gender));
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            throw new ArgumentException("A valid contact email address is required for patient registration.", nameof(request.Email));
        }

        var patient = Patient.Register(
            request.MedicalRecordNumber,
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            normalizedGender,
            request.Email.Trim(),
            request.PhoneNumber?.Trim());

        _context.Patients.Add(patient);
        await _context.SaveChangesAsync(cancellationToken);

        return patient.Id.Value;
    }
}