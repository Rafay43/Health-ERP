using System.Net;
using System.Net.Mail;
using HealthERP.Application.Common.Configurations;
using HealthERP.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using HealthERP.Application.PatientAccount;

namespace HealthERP.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;
    private readonly IHostEnvironment _environment;

    public EmailService(
        IOptions<EmailSettings> emailSettings,
        ILogger<EmailService> logger,
        IHostEnvironment environment)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
        _environment = environment;
    }

    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string userFirstName,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        var encodedToken = Uri.EscapeDataString(resetToken);
        var encodedEmail = Uri.EscapeDataString(toEmail);
        var resetLink = $"{_emailSettings.FrontendBaseUrl}/reset-password?token={encodedToken}&email={encodedEmail}";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #F8FAFC; color: #0F172A; margin: 0; padding: 0; }}
    .container {{ max-width: 560px; margin: 30px auto; background: #FFFFFF; border: 1px solid #E2E8F0; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 12px rgba(15,23,42,0.05); }}
    .header {{ background-color: #0F766E; padding: 24px; text-align: center; color: #FFFFFF; }}
    .header h1 {{ margin: 0; font-size: 20px; font-weight: 700; letter-spacing: -0.5px; }}
    .header p {{ margin: 4px 0 0 0; font-size: 12px; color: #CCFBF1; opacity: 0.9; }}
    .body {{ padding: 32px 24px; text-align: left; line-height: 1.6; font-size: 14px; color: #334155; }}
    .btn-container {{ text-align: center; margin: 28px 0; }}
    .btn {{ background-color: #0F766E; color: #FFFFFF !important; text-decoration: none; padding: 12px 28px; border-radius: 10px; font-weight: 700; font-size: 14px; display: inline-block; box-shadow: 0 2px 6px rgba(15,118,110,0.25); }}
    .footer {{ background-color: #F1F5F9; padding: 16px 24px; font-size: 12px; color: #64748B; text-align: center; border-top: 1px solid #E2E8F0; }}
    .link-box {{ background: #F8FAFC; border: 1px border #E2E8F0; padding: 10px; border-radius: 8px; word-break: break-all; font-family: monospace; font-size: 11px; color: #0F766E; margin-top: 12px; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>Health ERP</h1>
      <p>Hospital Management System</p>
    </div>
    <div class=""body"">
      <h2 style=""font-size: 18px; font-weight: 700; color: #0F172A; margin-top: 0;"">Reset Your Password</h2>
      <p>Hello {(string.IsNullOrWhiteSpace(userFirstName) ? "User" : userFirstName)},</p>
      <p>We received a request to reset the password for your Health ERP account. Click the button below to choose a new password:</p>
      <div class=""btn-container"">
        <a href=""{resetLink}"" class=""btn"">Reset Password</a>
      </div>
      <p style=""font-size: 13px; color: #64748B;"">This link will expire in 30 minutes for security reasons.</p>
      <p style=""font-size: 13px; color: #64748B;"">If you did not request a password reset, you can safely ignore this email. Your account remains secure.</p>
      <div class=""link-box"">{resetLink}</div>
    </div>
    <div class=""footer"">
      &copy; {DateTime.UtcNow.Year} Health ERP &bull; Secure Encrypted Password Recovery
    </div>
  </div>
</body>
</html>";

        try
        {
            if (!string.IsNullOrWhiteSpace(_emailSettings.Host) && _emailSettings.Host != "localhost")
            {
                using var message = new MailMessage();
                message.From = new MailAddress(_emailSettings.FromAddress, _emailSettings.FromName);
                message.To.Add(new MailAddress(toEmail));
                message.Subject = "Reset your Health ERP password";
                message.Body = htmlBody;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(_emailSettings.Host, _emailSettings.Port);
                client.EnableSsl = _emailSettings.EnableSsl;
                if (!string.IsNullOrWhiteSpace(_emailSettings.Username))
                {
                    client.Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password);
                }

                await client.SendMailAsync(message, cancellationToken);
                _logger.LogInformation("Password reset email sent to {Email}", toEmail);
            }
            else
            {
                _logger.LogInformation("Simulated sending password reset email to {Email}. Reset Link: {ResetLink}", toEmail, resetLink);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}. Reset link was: {ResetLink}", toEmail, resetLink);
        }
    }

    public async Task SendPatientActivationCodeEmailAsync(
        string toEmail,
        string patientName,
        string verificationCode,
        CancellationToken cancellationToken = default)
    {
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #F8FAFC; color: #0F172A; margin: 0; padding: 0; }}
    .container {{ max-width: 560px; margin: 30px auto; background: #FFFFFF; border: 1px solid #E2E8F0; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 12px rgba(15,23,42,0.05); }}
    .header {{ background-color: #0F766E; padding: 24px; text-align: center; color: #FFFFFF; }}
    .header h1 {{ margin: 0; font-size: 20px; font-weight: 700; letter-spacing: -0.5px; }}
    .body {{ padding: 32px 24px; text-align: left; line-height: 1.6; font-size: 14px; color: #334155; }}
    .code-box {{ background: #F1F5F9; border: 1px border #CBD5E1; padding: 16px; border-radius: 12px; text-align: center; font-family: monospace; font-size: 28px; font-weight: 700; letter-spacing: 6px; color: #0F766E; margin: 20px 0; }}
    .footer {{ background-color: #F1F5F9; padding: 16px 24px; font-size: 12px; color: #64748B; text-align: center; border-top: 1px solid #E2E8F0; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>Health ERP Patient Portal</h1>
    </div>
    <div class=""body"">
      <p>Hello <strong>{WebUtility.HtmlEncode(patientName)}</strong>,</p>
      <p>Use the 6-digit verification code below to connect your Health ERP account with your medical patient record:</p>
      <div class=""code-box"">{verificationCode}</div>
      <p>This code will expire in <strong>10 minutes</strong>. Do not share this code with anyone.</p>
    </div>
    <div class=""footer"">
      <p>&copy; 2026 Health ERP. All rights reserved.</p>
    </div>
  </div>
</body>
</html>";

        try
        {
            if (!string.IsNullOrWhiteSpace(_emailSettings.Host) && _emailSettings.Host != "localhost")
            {
                using var message = new MailMessage();
                message.From = new MailAddress(_emailSettings.FromAddress, _emailSettings.FromName);
                message.To.Add(new MailAddress(toEmail));
                message.Subject = "Your Health ERP Patient Portal Verification Code";
                message.Body = htmlBody;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(_emailSettings.Host, _emailSettings.Port);
                client.EnableSsl = _emailSettings.EnableSsl;
                if (!string.IsNullOrWhiteSpace(_emailSettings.Username))
                {
                    client.Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password);
                }

                await client.SendMailAsync(message, cancellationToken);
                _logger.LogInformation("Patient activation verification code email successfully sent to {Email}", toEmail);
            }
            else
            {
                if (!_environment.IsDevelopment())
                    throw new InvalidOperationException("Activation email delivery is not configured.");
                _logger.LogWarning("[DEV SIMULATION MODE] Patient Activation Verification Code for {Email} ({PatientName}): [ {Code} ]", toEmail, patientName, verificationCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send patient activation email to {Email}", toEmail);
            throw new ActivationEmailDeliveryException(ex);
        }
    }
}
