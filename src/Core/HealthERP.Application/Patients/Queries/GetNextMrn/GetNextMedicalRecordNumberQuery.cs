using MediatR;

namespace HealthERP.Application.Patients.Queries.GetNextMrn;

public record GetNextMedicalRecordNumberQuery : IRequest<string>;