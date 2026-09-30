using MediatR;
using HealthERP.Application.Laboratory.Models;

namespace HealthERP.Application.Laboratory.Queries.GetLabOrders;

public record GetLabOrdersQuery(
    Guid? PatientId = null,
    string? Status = null,
    string? Priority = null,
    bool AbnormalOnly = false,
    int Page = 1,
    int PageSize = 50
) : IRequest<List<LabOrderDto>>;
