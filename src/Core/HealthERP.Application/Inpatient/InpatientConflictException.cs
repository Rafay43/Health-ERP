namespace HealthERP.Application.Inpatient;

public sealed class InpatientConflictException(string message) : Exception(message);
