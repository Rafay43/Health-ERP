namespace HealthERP.Application.Billing;

public sealed class BillingPaymentStorageException(Exception innerException)
    : Exception("Unable to confirm payment persistence.", innerException);
