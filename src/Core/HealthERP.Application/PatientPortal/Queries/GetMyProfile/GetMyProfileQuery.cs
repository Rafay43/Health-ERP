using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientPortal.Models;
using MediatR;

namespace HealthERP.Application.PatientPortal.Queries.GetMyProfile;

public record GetMyProfileQuery : IRequest<PatientPortalProfileDto>;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, PatientPortalProfileDto>
{
    private readonly ICurrentPatientService _currentPatientService;

    public GetMyProfileQueryHandler(ICurrentPatientService currentPatientService)
    {
        _currentPatientService = currentPatientService;
    }

    public async Task<PatientPortalProfileDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var patient = await _currentPatientService.GetCurrentPatientAsync(cancellationToken);

        if (patient == null)
        {
            throw new UnauthorizedAccessException("Patient portal activation is required to access medical profile.");
        }

        var age = DateTime.UtcNow.Year - patient.DateOfBirth.Year;
        if (patient.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-age))) age--;

        return new PatientPortalProfileDto(
            patient.MedicalRecordNumber,
            patient.FirstName,
            patient.LastName,
            $"{patient.FirstName} {patient.LastName}".Trim(),
            patient.DateOfBirth,
            Math.Max(0, age),
            patient.Gender,
            patient.PhoneNumber,
            patient.Email);
    }
}
