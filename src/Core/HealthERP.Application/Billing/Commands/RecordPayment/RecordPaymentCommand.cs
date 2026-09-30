using MediatR;
using HealthERP.Domain.Billing;

namespace HealthERP.Application.Billing.Commands.RecordPayment;

public record RecordPaymentCommand(
    Guid InvoiceId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber = null,
    string? Notes = null
) : IRequest;
