using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientAccount.Models;
using MediatR;

namespace HealthERP.Application.PatientAccount.Commands.VerifyPatientActivation;

public class VerifyPatientActivationCommandHandler(IPatientActivationService activation) : IRequestHandler<VerifyPatientActivationCommand, VerifyActivationResponseDto>
{
    public Task<VerifyActivationResponseDto> Handle(VerifyPatientActivationCommand request, CancellationToken cancellationToken) =>
        activation.VerifyAsync(request.Code, cancellationToken);
}
