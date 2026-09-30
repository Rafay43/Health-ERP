using MediatR;
using HealthERP.Application.Patients.Models;

namespace HealthERP.Application.Patients.Queries.GetPatientEhr;

public record GetPatientEhrQuery(Guid PatientId) : IRequest<PatientEhrDto>;
