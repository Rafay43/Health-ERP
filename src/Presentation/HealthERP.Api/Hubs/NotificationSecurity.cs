using System.Security.Claims;
using HealthERP.Application.Common.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;

namespace HealthERP.Api.Hubs;

public static class NotificationSecurity
{
    public const string OwnerPolicy = "NotificationOwner";

    public static IServiceCollection AddNotificationSecurity(this IServiceCollection services)
    {
        services.AddAuthorization(options => options.AddPolicy(OwnerPolicy, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                && id != Guid.Empty)));
        services.AddSingleton<IUserIdProvider, NotificationUserIdProvider>();
        return services;
    }

    public static void MapNotificationHubs(this IEndpointRouteBuilder app)
    {
        app.MapHub<NotificationHub>(NotificationHub.HubUrl, options => options.CloseOnAuthenticationExpiration = true)
            .RequireAuthorization(OwnerPolicy);
        app.MapHub<AppointmentHub>(AppointmentHub.HubUrl, options => options.CloseOnAuthenticationExpiration = true)
            .RequireAuthorization(Policies.CanViewPatients);
    }
}

public sealed class NotificationUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        Guid.TryParse(connection.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty
            ? id.ToString()
            : null;
}

// Browser WebSocket/SSE transports send the bearer token in the query string.
// Accept it only on the authenticated hub routes, never on regular API routes.
public sealed class SignalRJwtBearerEvents : JwtBearerEvents
{
    public override Task MessageReceived(MessageReceivedContext context)
    {
        var path = context.HttpContext.Request.Path;
        if (path.StartsWithSegments(NotificationHub.HubUrl) || path.StartsWithSegments(AppointmentHub.HubUrl))
        {
            var token = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token)) context.Token = token;
        }
        return Task.CompletedTask;
    }
}
