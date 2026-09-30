using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Billing.Models;
using HealthERP.Domain.Patients;

namespace HealthERP.Application.Billing.Queries.GetInvoices;

public class GetInvoicesQueryHandler : IRequestHandler<GetInvoicesQuery, List<InvoiceDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInvoicesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<InvoiceDto>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Invoices
            .Include(i => i.Patient)
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .AsNoTracking();

        if (request.PatientId.HasValue)
        {
            var pId = new PatientId(request.PatientId.Value);
            query = query.Where(i => i.PatientId == pId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(i => i.Status.ToString().ToLower() == request.Status.ToLower());
        }

        var invoices = await query
            .OrderByDescending(i => i.IssueDateUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return invoices.Select(i => new InvoiceDto(
            i.Id.Value,
            i.PatientId.Value,
            i.Patient != null ? $"{i.Patient.FirstName} {i.Patient.LastName}" : "Unknown Patient",
            i.AppointmentId?.Value,
            i.InvoiceNumber,
            i.IssueDateUtc,
            i.DueDateUtc,
            i.Status.ToString(),
            i.TotalAmount,
            i.PaidAmount,
            i.BalanceDue,
            i.LineItems.Select(l => new InvoiceLineItemDto(
                l.Id,
                l.Description,
                l.Quantity,
                l.UnitPrice,
                l.LineTotal
            )).ToList(),
            i.Payments.Select(p => new PaymentDto(
                p.Id,
                p.PaymentDateUtc,
                p.Amount,
                p.PaymentMethod.ToString(),
                p.ReferenceNumber,
                p.Notes
            )).ToList()
        )).ToList();
    }
}
