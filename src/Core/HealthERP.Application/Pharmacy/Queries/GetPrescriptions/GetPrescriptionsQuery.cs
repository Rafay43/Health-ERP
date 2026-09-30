using MediatR;
using HealthERP.Application.Pharmacy.Models;

namespace HealthERP.Application.Pharmacy.Queries.GetPrescriptions;

public record GetPrescriptionsQuery(
    Guid? PatientId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<List<PrescriptionDto>>;
