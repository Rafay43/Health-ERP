using MediatR;

namespace HealthERP.Application.Patients.Queries.GetPatients;

public record GetPatientsQuery(int Page = 1, int PageSize = 25) : IRequest<List<PatientDto>>;