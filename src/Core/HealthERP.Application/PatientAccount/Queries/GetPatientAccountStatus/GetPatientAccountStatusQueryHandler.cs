using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientAccount.Models;
using MediatR;

namespace HealthERP.Application.PatientAccount.Queries.GetPatientAccountStatus;

public class GetPatientAccountStatusQueryHandler : IRequestHandler<GetPatientAccountStatusQuery, PatientAccountStatusDto>
{
    private readonly ICurrentPatientService _currentPatientService;

    public GetPatientAccountStatusQueryHandler(ICurrentPatientService currentPatientService)
    {
        _currentPatientService = currentPatientService;
    }

    public async Task<PatientAccountStatusDto> Handle(GetPatientAccountStatusQuery request, CancellationToken cancellationToken)
    {
        var patient = await _currentPatientService.GetCurrentPatientAsync(cancellationToken);

        if (patient == null)
        {
            return new PatientAccountStatusDto(false, null);
        }

        return new PatientAccountStatusDto(
            true,
            new LinkedPatientSummaryDto(
                patient.MedicalRecordNumber,
                $"{patient.FirstName} {patient.LastName}".Trim()));
    }
}
