using HealthERP.Application.PatientAccount.Models;

namespace HealthERP.Application.Common.Interfaces;

public interface IPatientActivationService
{
    Task<StartActivationResponseDto> StartAsync(string mrn, DateOnly dateOfBirth, CancellationToken cancellationToken = default);
    Task<VerifyActivationResponseDto> VerifyAsync(string code, CancellationToken cancellationToken = default);
}
