namespace HealthERP.Application.Billing;

public sealed class BillingPaymentConflictException(string message) : Exception(message);
