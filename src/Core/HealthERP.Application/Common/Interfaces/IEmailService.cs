namespace HealthERP.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(
        string toEmail,
        string userFirstName,
        string resetToken,
        CancellationToken cancellationToken = default);

    Task SendPatientActivationCodeEmailAsync(
        string toEmail,
        string patientName,
        string verificationCode,
        CancellationToken cancellationToken = default);
}
