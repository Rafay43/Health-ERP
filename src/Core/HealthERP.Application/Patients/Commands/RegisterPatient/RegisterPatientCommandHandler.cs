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

    public async Task<Guid> Handle(RegisterPatientCommand request, CancellationToken cancellationToken)
    {
        var patient = Patient.Register(
            request.MedicalRecordNumber,
            request.FirstName,
            request.LastName,
            request.DateOfBirth);

        _context.Patients.Add(patient);
        await _context.SaveChangesAsync(cancellationToken);

        return patient.Id.Value;
    }
}