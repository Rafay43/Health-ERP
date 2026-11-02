namespace HealthERP.Application.PatientAccount;

public sealed class ActivationConflictException : Exception;
public sealed class ActivationStorageException(Exception inner) : Exception("Unable to confirm activation persistence.", inner);
public sealed class ActivationEmailDeliveryException(Exception inner) : Exception("Unable to deliver activation email.", inner);
