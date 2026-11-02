using MediatR;

namespace HealthERP.Application.Patients.Commands.UpdatePatient;

public record UpdatePatientCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? Gender = null,
    string? Email = null,
    string? PhoneNumber = null) : IRequest<bool>;
