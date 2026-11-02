using HealthERP.Application.PatientAccount.Models;
using MediatR;

namespace HealthERP.Application.PatientAccount.Commands.StartPatientActivation;

public record StartPatientActivationCommand(
    string MedicalRecordNumber,
    DateOnly DateOfBirth) : IRequest<StartActivationResponseDto>;
