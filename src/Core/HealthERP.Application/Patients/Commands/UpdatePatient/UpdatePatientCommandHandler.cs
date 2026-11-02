using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Patients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Patients.Commands.UpdatePatient;

public class UpdatePatientCommandHandler : IRequestHandler<UpdatePatientCommand, bool>
{
    private readonly IApplicationDbContext _context;

    private static readonly string[] AllowedGenders = ["Male", "Female", "Other", "Prefer not to say"];

    public UpdatePatientCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdatePatientCommand request, CancellationToken cancellationToken)
    {
        var pId = new PatientId(request.Id);
        var patient = await _context.Patients
            .FirstOrDefaultAsync(p => p.Id == pId, cancellationToken);

        if (patient == null)
        {
            throw new KeyNotFoundException($"Patient with ID '{request.Id}' was not found.");
        }

        string? normalizedGender = null;
        if (!string.IsNullOrWhiteSpace(request.Gender))
        {
            normalizedGender = AllowedGenders.FirstOrDefault(g => g.Equals(request.Gender.Trim(), StringComparison.OrdinalIgnoreCase));
            if (normalizedGender == null)
            {
                throw new ArgumentException($"Invalid gender specified. Allowed values: {string.Join(", ", AllowedGenders)}.", nameof(request.Gender));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
        {
            throw new ArgumentException("Provided contact email format is invalid.", nameof(request.Email));
        }

        patient.UpdateDetails(
            request.FirstName,
            request.LastName,
            normalizedGender,
            request.Email,
            request.PhoneNumber);

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
