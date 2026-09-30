using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
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
        // 1. Seed Roles
        var defaultRoles = new[] { "Admin", "Doctor", "Nurse", "Staff" };

        foreach (var roleName in defaultRoles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new ApplicationRole { Name = roleName });
            }
        }

        // 2. Seed Default Admin User
        var adminEmail = "admin@healtherp.com";
        var administrator = await _userManager.FindByEmailAsync(adminEmail);

        if (administrator is null)
        {
            administrator = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Administrator",
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(administrator, string.Empty);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(administrator, "Admin");
                _logger.LogInformation("Default Admin user created successfully: {Email}", adminEmail);
            }
        }

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
    }
}
