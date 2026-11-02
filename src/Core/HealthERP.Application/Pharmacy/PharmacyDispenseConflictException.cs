namespace HealthERP.Application.Pharmacy;

public sealed class PharmacyDispenseConflictException(string message) : Exception(message);
