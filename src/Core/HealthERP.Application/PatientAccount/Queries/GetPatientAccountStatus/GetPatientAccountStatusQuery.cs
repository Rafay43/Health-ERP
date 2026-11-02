using HealthERP.Application.PatientAccount.Models;
using MediatR;

namespace HealthERP.Application.PatientAccount.Queries.GetPatientAccountStatus;

public record GetPatientAccountStatusQuery : IRequest<PatientAccountStatusDto>;
