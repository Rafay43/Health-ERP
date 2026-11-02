namespace HealthERP.Application.Common.Configurations;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string FromName { get; set; } = "Health ERP Support";
    public string FromAddress { get; set; } = "noreply@example.org";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = false;
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
}
