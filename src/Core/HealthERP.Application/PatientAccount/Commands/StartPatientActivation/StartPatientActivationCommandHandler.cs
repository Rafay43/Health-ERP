using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientAccount.Models;
using MediatR;

namespace HealthERP.Application.PatientAccount.Commands.StartPatientActivation;

public class StartPatientActivationCommandHandler(IPatientActivationService activation) : IRequestHandler<StartPatientActivationCommand, StartActivationResponseDto>
{
    public Task<StartActivationResponseDto> Handle(StartPatientActivationCommand request, CancellationToken cancellationToken) =>
        activation.StartAsync(request.MedicalRecordNumber, request.DateOfBirth, cancellationToken);
}
