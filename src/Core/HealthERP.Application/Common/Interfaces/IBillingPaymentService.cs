using HealthERP.Domain.Billing;

namespace HealthERP.Application.Common.Interfaces;

public interface IBillingPaymentService
{
    Task RecordAsync(Guid invoiceId, decimal amount, PaymentMethod method,
        string? referenceNumber = null, string? notes = null, CancellationToken cancellationToken = default);
}
