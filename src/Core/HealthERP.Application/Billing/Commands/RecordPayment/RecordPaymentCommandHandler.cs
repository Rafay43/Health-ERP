using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Billing;

namespace HealthERP.Application.Billing.Commands.RecordPayment;

public class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand>
{
    private readonly IApplicationDbContext _context;

    public RecordPaymentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        var invoiceId = new InvoiceId(request.InvoiceId);

        var invoice = await _context.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new KeyNotFoundException($"Invoice with ID '{request.InvoiceId}' was not found.");
        }

        invoice.RecordPayment(
            request.Amount,
            request.PaymentMethod,
            request.ReferenceNumber,
            request.Notes);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
