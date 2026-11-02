using System.Security.Claims;
using System.Text;
using HealthERP.Api.Hubs;
using HealthERP.Application.Common.Configurations;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace HealthERP.Api.Authentication;

public static class AccountJwtAuthentication
{
    public static IServiceCollection AddAccountJwtAuthentication(this IServiceCollection services, JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Secret) || Encoding.UTF8.GetByteCount(settings.Secret) < 32 || settings.Secret.StartsWith("<"))
            throw new InvalidOperationException("Configure JwtSettings:Secret with at least 32 bytes through user secrets or environment configuration.");
        if (string.IsNullOrWhiteSpace(settings.Issuer) || string.IsNullOrWhiteSpace(settings.Audience))
            throw new InvalidOperationException("Configure JwtSettings:Issuer and JwtSettings:Audience.");
        services.AddScoped<AccountJwtBearerEvents>();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.EventsType = typeof(AccountJwtBearerEvents);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = settings.Issuer, ValidAudience = settings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret))
            };
        });
        return services;
    }
}

public sealed class AccountJwtBearerEvents(IJwtSessionValidator sessionValidator) : JwtBearerEvents
{
    private readonly SignalRJwtBearerEvents _hubTransport = new();

    public override Task MessageReceived(MessageReceivedContext context) => _hubTransport.MessageReceived(context);

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var version = context.Principal?.FindFirstValue(JwtSessionVersion.ClaimName);
        if (!Guid.TryParse(userId, out var id) || !await sessionValidator.ValidateAsync(id, version, context.HttpContext.RequestAborted))
            context.Fail("The authenticated session is no longer valid.");
    }
}
