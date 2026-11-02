using MediatR;
using HealthERP.Application.Common.Interfaces;

namespace HealthERP.Application.Billing.Commands.RecordPayment;

public class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand>
{
    private readonly IBillingPaymentService _payments;

    public RecordPaymentCommandHandler(IBillingPaymentService payments)
    {
        _payments = payments;
    }

    public async Task Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        await _payments.RecordAsync(request.InvoiceId, request.Amount, request.PaymentMethod,
            request.ReferenceNumber, request.Notes, cancellationToken);
    }
}
