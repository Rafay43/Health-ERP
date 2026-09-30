using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HealthERP.Infrastructure.Persistence;
using HealthERP.Infrastructure.Persistence.Interceptors;
using HealthERP.Infrastructure.Identity;
using HealthERP.Infrastructure.Services;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Configurations;

namespace HealthERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddTransient<IIdentityService, IdentityService>();
        services.AddHostedService<NoShowTrackingWorker>();

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddScoped<ApplicationDbContextInitialiser>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            options.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        return services;
    }
}