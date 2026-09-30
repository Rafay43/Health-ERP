using MediatR;
using HealthERP.Application.Billing.Models;

namespace HealthERP.Application.Billing.Queries.GetInvoices;

public record GetInvoicesQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null,
    Guid? PatientId = null
) : IRequest<List<InvoiceDto>>;
