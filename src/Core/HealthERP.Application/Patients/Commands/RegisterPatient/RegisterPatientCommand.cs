using MediatR;

namespace HealthERP.Application.Patients.Commands.RegisterPatient;

public record RegisterPatientCommand(
    string MedicalRecordNumber,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Gender,
    string Email,
    string? PhoneNumber = null) : IRequest<Guid>;