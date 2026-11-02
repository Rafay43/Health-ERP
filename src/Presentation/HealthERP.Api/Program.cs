using HealthERP.Api.Authentication;
using HealthERP.Application;
using HealthERP.Infrastructure;
using HealthERP.Infrastructure.Persistence;
using HealthERP.Api.Endpoints;
using HealthERP.Api.Middleware;
using HealthERP.Application.Common.Configurations;
using HealthERP.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? new JwtSettings();

builder.Services.AddAccountJwtAuthentication(jwtSettings);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(HealthERP.Application.Common.Security.Policies.CanViewPatients, policy =>
        policy.RequireRole(HealthERP.Application.Common.Security.Roles.Admin, HealthERP.Application.Common.Security.Roles.Doctor, HealthERP.Application.Common.Security.Roles.Nurse, HealthERP.Application.Common.Security.Roles.Receptionist));

    options.AddPolicy(HealthERP.Application.Common.Security.Policies.CanEditPatients, policy =>
        policy.RequireRole(HealthERP.Application.Common.Security.Roles.Admin, HealthERP.Application.Common.Security.Roles.Doctor, HealthERP.Application.Common.Security.Roles.Nurse, HealthERP.Application.Common.Security.Roles.Receptionist));

    options.AddPolicy(HealthERP.Application.Common.Security.Policies.CanPrescribeMedication, policy =>
        policy.RequireRole(HealthERP.Application.Common.Security.Roles.Admin, HealthERP.Application.Common.Security.Roles.Doctor));

    options.AddPolicy(HealthERP.Application.Common.Security.Policies.CanDispenseMedication, policy =>
        policy.RequireRole(HealthERP.Application.Common.Security.Roles.Admin, HealthERP.Application.Common.Security.Roles.Pharmacist));

    options.AddPolicy(HealthERP.Application.Common.Security.Policies.CanOrderLabTest, policy =>
        policy.RequireRole(HealthERP.Application.Common.Security.Roles.Admin, HealthERP.Application.Common.Security.Roles.Doctor));

    options.AddPolicy(HealthERP.Application.Common.Security.Policies.CanEnterLabResult, policy =>
        policy.RequireRole(HealthERP.Application.Common.Security.Roles.Admin, HealthERP.Application.Common.Security.Roles.LabTechnician));

    options.AddPolicy(HealthERP.Application.Common.Security.Policies.CanManageAdmissions, policy =>
        policy.RequireRole(HealthERP.Application.Common.Security.Roles.Admin, HealthERP.Application.Common.Security.Roles.Nurse, HealthERP.Application.Common.Security.Roles.Receptionist));

    options.AddPolicy(HealthERP.Application.Common.Security.Policies.CanManageBilling, policy =>
        policy.RequireRole(HealthERP.Application.Common.Security.Roles.Admin, HealthERP.Application.Common.Security.Roles.BillingStaff));

    options.AddPolicy(HealthERP.Application.Common.Security.Policies.CanManageStaff, policy =>
        policy.RequireRole(HealthERP.Application.Common.Security.Roles.Admin));

    options.AddPolicy(HealthERP.Application.Common.Security.Policies.CanViewExecutiveReports, policy =>
        policy.RequireRole(HealthERP.Application.Common.Security.Roles.Admin));
});
builder.Services.AddSignalR();
builder.Services.AddNotificationSecurity();
builder.Services.AddScoped<HealthERP.Application.Common.Interfaces.IPrivateNotificationPublisher, HealthERP.Api.Services.NotificationPublisher>();
builder.Services.AddScoped<HealthERP.Application.Common.Interfaces.IAppointmentNotificationService, HealthERP.Api.Services.AppointmentNotificationService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseCors("AllowAngularDev");

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.InitialiseDatabaseAsync();
}

app.UseHttpsRedirection();

app.MapAuthEndpoints();
app.MapPatientAccountEndpoints();
app.MapPatientPortalEndpoints();
app.MapPatientEndpoints();
app.MapStaffEndpoints();
app.MapAppointmentEndpoints();
app.MapDoctorEndpoints();
app.MapBillingEndpoints();
app.MapPharmacyEndpoints();
app.MapLaboratoryEndpoints();
app.MapInpatientEndpoints();
app.MapEmergencyEndpoints();
app.MapReportsEndpoints();
app.MapAdminEndpoints();
app.MapNotificationEndpoints();
app.MapDashboardEndpoints();
app.MapNotificationHubs();

app.Run();
