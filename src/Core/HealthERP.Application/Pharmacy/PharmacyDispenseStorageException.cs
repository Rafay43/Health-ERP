namespace HealthERP.Application.Pharmacy;

public sealed class PharmacyDispenseStorageException(Exception innerException)
    : Exception("Unable to confirm dispensing. Refresh the prescription and inventory before retrying.", innerException);
