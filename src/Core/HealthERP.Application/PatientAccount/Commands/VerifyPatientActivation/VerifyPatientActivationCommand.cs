using HealthERP.Application.PatientAccount.Models;
using MediatR;

namespace HealthERP.Application.PatientAccount.Commands.VerifyPatientActivation;

public record VerifyPatientActivationCommand(
    string Code) : IRequest<VerifyActivationResponseDto>;
