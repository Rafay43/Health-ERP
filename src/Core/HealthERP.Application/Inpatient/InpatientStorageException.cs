namespace HealthERP.Application.Inpatient;

public sealed class InpatientStorageException(Exception innerException)
    : Exception("Unable to confirm inpatient transition persistence.", innerException);
