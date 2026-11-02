using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using HealthERP.Infrastructure.Identity;

namespace HealthERP.Infrastructure.Persistence;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
}

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        if (!_environment.IsDevelopment())
            throw new InvalidOperationException("Demo initialization is restricted to Development.");

        // 1. Seed Security Roles (Idempotent)
        var defaultRoles = new[]
        {
            HealthERP.Application.Common.Security.Roles.Admin,
            HealthERP.Application.Common.Security.Roles.Doctor,
            HealthERP.Application.Common.Security.Roles.Nurse,
            HealthERP.Application.Common.Security.Roles.Pharmacist,
            HealthERP.Application.Common.Security.Roles.LabTechnician,
            HealthERP.Application.Common.Security.Roles.Receptionist,
            HealthERP.Application.Common.Security.Roles.BillingStaff,
            HealthERP.Application.Common.Security.Roles.Patient,
            "Staff"
        };

        foreach (var roleName in defaultRoles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new ApplicationRole { Name = roleName });
            }
        }

        // Synthetic demo accounts/catalogs require explicit opt-in and external credentials.
        if (!_configuration.GetValue<bool>("DevelopmentSeed:Enabled")) return;
        var adminEmail = _configuration["DevelopmentSeed:AdminEmail"];
        var adminPassword = _configuration["DevelopmentSeed:AdminPassword"];
        var doctorEmail = _configuration["DevelopmentSeed:DoctorEmail"];
        var doctorPassword = _configuration["DevelopmentSeed:DoctorPassword"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException("DevelopmentSeed requires AdminEmail and AdminPassword from local configuration.");
        if (string.IsNullOrWhiteSpace(doctorEmail) != string.IsNullOrWhiteSpace(doctorPassword))
            throw new InvalidOperationException("Configure both DevelopmentSeed doctor fields or neither.");

        await CreateDemoAccountAsync(adminEmail, adminPassword, "System", "Administrator", HealthERP.Application.Common.Security.Roles.Admin);
        if (!string.IsNullOrWhiteSpace(doctorEmail))
            await CreateDemoAccountAsync(doctorEmail, doctorPassword!, "Demo", "Doctor", HealthERP.Application.Common.Security.Roles.Doctor);

        // 3. Seed Initial Pharmacy Inventory
        if (!await _context.Drugs.AnyAsync())
        {
            var initialDrugs = new[]
            {
                HealthERP.Domain.Pharmacy.Drug.Create("Amoxicillin 500mg", "Amoxicillin", "Antibiotics", 150, 30, DateTime.UtcNow.AddMonths(18), 12.50m),
                HealthERP.Domain.Pharmacy.Drug.Create("Paracetamol 500mg", "Acetaminophen", "Analgesics", 500, 100, DateTime.UtcNow.AddMonths(24), 5.00m),
                HealthERP.Domain.Pharmacy.Drug.Create("Metformin 850mg", "Metformin Hydrochloride", "Antidiabetics", 20, 50, DateTime.UtcNow.AddMonths(12), 18.00m), // Low stock
                HealthERP.Domain.Pharmacy.Drug.Create("Atorvastatin 20mg", "Atorvastatin Calcium", "Cardiovascular", 80, 25, DateTime.UtcNow.AddDays(15), 24.00m), // Expiring soon
                HealthERP.Domain.Pharmacy.Drug.Create("Omeprazole 20mg", "Omeprazole", "Gastrointestinal", 200, 40, DateTime.UtcNow.AddMonths(20), 15.00m),
                HealthERP.Domain.Pharmacy.Drug.Create("Lisinopril 10mg", "Lisinopril", "Cardiovascular", 120, 30, DateTime.UtcNow.AddMonths(14), 11.00m),
                HealthERP.Domain.Pharmacy.Drug.Create("Albuterol Inhaler 90mcg", "Salbutamol", "Respiratory", 15, 20, DateTime.UtcNow.AddMonths(10), 32.50m) // Low stock
            };

            await _context.Drugs.AddRangeAsync(initialDrugs);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Initial pharmacy inventory seeded successfully.");
        }

        // 4. Seed Initial Laboratory Test Catalog
        if (!await _context.LabTests.AnyAsync())
        {
            var initialLabTests = new[]
            {
                HealthERP.Domain.Laboratory.LabTest.Create("Complete Blood Count (CBC)", "CBC-01", "Hematology", "4.5-11.0 x10^3/uL", "cells/uL", 45.00m, "Whole Blood"),
                HealthERP.Domain.Laboratory.LabTest.Create("Comprehensive Metabolic Panel (CMP)", "CMP-02", "Biochemistry", "70-99 mg/dL", "mg/dL", 65.00m, "Serum"),
                HealthERP.Domain.Laboratory.LabTest.Create("Lipid Profile Panel", "LIP-03", "Biochemistry", "< 200 mg/dL", "mg/dL", 55.00m, "Serum"),
                HealthERP.Domain.Laboratory.LabTest.Create("Glycated Hemoglobin (HbA1c)", "HBA1C-04", "Endocrinology", "< 5.7 %", "%", 40.00m, "Whole Blood"),
                HealthERP.Domain.Laboratory.LabTest.Create("Thyroid Stimulating Hormone (TSH)", "TSH-05", "Endocrinology", "0.4-4.0 mIU/L", "mIU/L", 50.00m, "Serum"),
                HealthERP.Domain.Laboratory.LabTest.Create("Urinalysis Routine", "UA-06", "Urinalysis", "Clear/Yellow", "N/A", 25.00m, "Urine")
            };

            await _context.LabTests.AddRangeAsync(initialLabTests);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Initial laboratory test catalog seeded successfully.");
        }

        // 5. Seed Initial Hospital Wards & Beds Catalog
        if (!await _context.Beds.AnyAsync())
        {
            var initialBeds = new[]
            {
                HealthERP.Domain.Inpatient.Bed.Create("ICU Ward", "ICU-101", "Bed-A", "ICU Special Bed", 350.00m),
                HealthERP.Domain.Inpatient.Bed.Create("ICU Ward", "ICU-101", "Bed-B", "ICU Special Bed", 350.00m),
                HealthERP.Domain.Inpatient.Bed.Create("General Ward A", "GW-201", "Bed-01", "Standard Hospital Bed", 100.00m),
                HealthERP.Domain.Inpatient.Bed.Create("General Ward A", "GW-201", "Bed-02", "Standard Hospital Bed", 100.00m),
                HealthERP.Domain.Inpatient.Bed.Create("General Ward A", "GW-202", "Bed-01", "Standard Hospital Bed", 100.00m),
                HealthERP.Domain.Inpatient.Bed.Create("Private Suite", "PS-301", "Bed-VIP", "Luxury Electric Bed", 500.00m),
                HealthERP.Domain.Inpatient.Bed.Create("Pediatric Ward", "PED-102", "Bed-01", "Pediatric Safety Bed", 120.00m),
                HealthERP.Domain.Inpatient.Bed.Create("Surgical Suite", "SURG-401", "Bed-01", "Post-Op Recovery Bed", 250.00m)
            };

            await _context.Beds.AddRangeAsync(initialBeds);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Initial hospital beds catalog seeded successfully.");
        }

        // 6. Seed Initial Emergency Triage Cases
        if (!await _context.TriageRecords.AnyAsync())
        {
            var firstPatient = await _context.Patients.FirstOrDefaultAsync();
            if (firstPatient != null)
            {
                var initialTriage = new[]
                {
                    HealthERP.Domain.Triage.TriageRecord.Create(
                        firstPatient.Id,
                        "Severe Chest Pain radiating to left arm",
                        "150/95",
                        110,
                        94,
                        37.5m,
                        22,
                        HealthERP.Domain.Triage.TriageSeverity.Level1_Resuscitation,
                        "Nurse Clara Barton",
                        "Trauma Bay 01"),
                    HealthERP.Domain.Triage.TriageRecord.Create(
                        firstPatient.Id,
                        "Acute shortness of breath and wheezing",
                        "135/85",
                        98,
                        92,
                        38.1m,
                        24,
                        HealthERP.Domain.Triage.TriageSeverity.Level2_Emergent,
                        "Nurse Clara Barton",
                        "ER Bay 03")
                };

                await _context.TriageRecords.AddRangeAsync(initialTriage);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Initial emergency triage cases seeded successfully.");
            }
        }
    }
    private async Task CreateDemoAccountAsync(string email, string password, string firstName, string lastName, string role)
    {
        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            _logger.LogWarning("A configured development account already exists; its credentials were not changed.");
            return;
        }
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FirstName = firstName, LastName = lastName,
            EmailConfirmed = true, CreatedAtUtc = DateTime.UtcNow
        };
        var created = await _userManager.CreateAsync(user, password);
        if (!created.Succeeded) throw new InvalidOperationException("Development account creation failed; check the configured password policy.");
        var assigned = await _userManager.AddToRoleAsync(user, role);
        if (!assigned.Succeeded) throw new InvalidOperationException("Development account role assignment failed.");
        _logger.LogInformation("Configured development account created.");
    }
}
