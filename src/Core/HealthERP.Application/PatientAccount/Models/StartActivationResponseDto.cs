namespace HealthERP.Application.PatientAccount.Models;

public record StartActivationResponseDto(
    bool Success,
    string Message,
    string? MaskedDestination = null,
    bool RequiresSupport = false,
    DateTime? ResendCooldownUntilUtc = null);

public record VerifyActivationResponseDto(
    bool Success,
    string Message,
    LinkedPatientSummaryDto? Patient = null);
