namespace HealthERP.Application.Common.Configurations;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; init; } = default!;
    public string Issuer { get; init; } = default!;
    public string Audience { get; init; } = default!;
    public int ExpiryMinutes { get; init; } = 60;
}
