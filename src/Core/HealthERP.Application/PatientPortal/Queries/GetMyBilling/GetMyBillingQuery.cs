using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientPortal.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.PatientPortal.Queries.GetMyBilling;

public record GetMyBillingQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null) : IRequest<List<PatientInvoiceDto>>;

public class GetMyBillingQueryHandler : IRequestHandler<GetMyBillingQuery, List<PatientInvoiceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentPatientService _currentPatientService;

    public GetMyBillingQueryHandler(
        IApplicationDbContext context,
        ICurrentPatientService currentPatientService)
    {
        _context = context;
        _currentPatientService = currentPatientService;
    }

    public async Task<List<PatientInvoiceDto>> Handle(GetMyBillingQuery request, CancellationToken cancellationToken)
    {
        var patient = await _currentPatientService.GetCurrentPatientAsync(cancellationToken);

        if (patient == null)
        {
            throw new UnauthorizedAccessException("Patient portal activation is required to access billing statements.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .Where(i => i.PatientId == patient.Id);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var statusStr = request.Status.Trim();
            if (Enum.TryParse<HealthERP.Domain.Billing.InvoiceStatus>(statusStr, true, out var parsedStatus))
            {
                query = query.Where(i => i.Status == parsedStatus);
            }
        }

        var invoices = await query
            .OrderByDescending(i => i.IssueDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return invoices.Select(i => new PatientInvoiceDto(
            i.Id.Value,
            i.InvoiceNumber,
            i.IssueDateUtc,
            i.DueDateUtc,
            i.Status.ToString(),
            i.TotalAmount,
            i.PaidAmount,
            i.BalanceDue,
            i.LineItems.Select(li => new PatientInvoiceLineItemDto(
                li.Description,
                li.Quantity,
                li.UnitPrice,
                li.LineTotal)).ToList(),
            i.Payments.Select(p => new PatientPaymentDto(
                p.Amount,
                p.PaymentMethod.ToString(),
                p.PaymentDateUtc)).ToList()))
            .ToList();
    }
}
