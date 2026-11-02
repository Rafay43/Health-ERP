using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HealthERP.Api.Authentication;
using HealthERP.Api.Endpoints;
using HealthERP.Api.Middleware;
using HealthERP.Application;
using HealthERP.Application.Common.Configurations;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Security;
using HealthERP.Application.Pharmacy;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Pharmacy;
using HealthERP.Infrastructure.Persistence;
using HealthERP.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;

namespace HealthERP.Api.IntegrationTests;

[CollectionDefinition("Pharmacy PostgreSQL", DisableParallelization = true)]
public class PharmacyPostgresCollection : ICollectionFixture<PharmacyPostgresFixture>;

// No default ERP connection: explicit loopback maintenance connection is required.
// Every run creates/drops only its own randomly named, empty test database.
public sealed class PharmacyPostgresFixture : IAsyncLifetime
{
    private string _maintenance = null!;
    private string _database = null!;
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _maintenance = Environment.GetEnvironmentVariable("HEALTHERP_PHARMACY_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set HEALTHERP_PHARMACY_TEST_CONNECTION to an isolated local PostgreSQL maintenance connection; see docs/testing.md.");
        var connection = new NpgsqlConnectionStringBuilder(_maintenance);
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database != "postgres")
            throw new InvalidOperationException("Pharmacy tests require an explicit loopback postgres maintenance connection, never the ERP database.");
        _database = "health_erp_pharmacy_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(_maintenance);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await create.ExecuteNonQueryAsync();
        connection.Database = _database;
        ConnectionString = connection.ConnectionString;
        await using var context = Context();
        await context.Database.EnsureCreatedAsync();
    }

    public ApplicationDbContext Context(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(ConnectionString).AddInterceptors(interceptors).Options);

    public async Task DisposeAsync()
    {
        if (ConnectionString is null || _database is null) return;
        if (!_database.StartsWith("health_erp_pharmacy_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to drop a non-test database.");
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_maintenance);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_database}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

[Collection("Pharmacy PostgreSQL")]
public class PharmacyDispensingTests(PharmacyPostgresFixture database)
{
    private async Task<(Drug Drug, Prescription Rx)> Seed(int stock, bool linked = true, bool cancelled = false)
    {
        await using var context = database.Context();
        var patient = Patient.Register("T-" + Guid.NewGuid().ToString("N")[..16], "Test", "Patient", new DateOnly(1990, 1, 1));
        patient.CreatedAtUtc = DateTime.UtcNow;
        var drug = Drug.Create("Test medication", "Test", "Test", stock, 0, DateTime.UtcNow.AddYears(1), 1m);
        drug.CreatedAtUtc = DateTime.UtcNow;
        var rx = Prescription.Create(patient.Id, linked ? drug.Id : null, "Test Doctor", drug.Name,
            "Text is not a stock quantity", "Text is not stock frequency", 7, 0, null);
        rx.CreatedAtUtc = DateTime.UtcNow;
        if (cancelled) rx.Cancel();
        context.Patients.Add(patient); context.Drugs.Add(drug); context.Prescriptions.Add(rx);
        await context.SaveChangesAsync();
        return (drug, rx);
    }

    private async Task AssertState(DrugId drugId, PrescriptionId prescriptionId, int quantity, PrescriptionStatus status)
    {
        await using var fresh = database.Context();
        Assert.Equal(quantity, (await fresh.Drugs.SingleAsync(d => d.Id == drugId)).StockQuantity);
        var rx = await fresh.Prescriptions.SingleAsync(p => p.Id == prescriptionId);
        Assert.Equal(status, rx.Status);
        Assert.Equal(status == PrescriptionStatus.Dispensed, rx.DispensedDateUtc.HasValue);
    }

    [Theory]
    [InlineData(10, 3, 7, true)]
    [InlineData(3, 3, 0, true)]
    [InlineData(2, 3, 2, false)]
    [InlineData(0, 3, 0, false)]
    public async Task StockBoundary_IsAtomic(int stock, int requested, int remaining, bool succeeds)
    {
        var (drug, rx) = await Seed(stock);
        await using var context = database.Context();
        var service = new PharmacyDispensingService(context);
        if (succeeds)
        {
            var result = await service.DispenseAsync(rx.Id.Value, requested);
            Assert.Equal("Dispensed", result.Status); Assert.NotNull(result.DispensedDateUtc);
        }
        else
        {
            await Assert.ThrowsAsync<PharmacyDispenseConflictException>(() => service.DispenseAsync(rx.Id.Value, requested));
            Assert.Empty(context.ChangeTracker.Entries());
            await context.SaveChangesAsync(); // No failed tracked mutation can leak into a subsequent save.
        }
        await AssertState(drug.Id, rx.Id, remaining, succeeds ? PrescriptionStatus.Dispensed : PrescriptionStatus.Pending);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveQuantity_IsRejectedWithoutDefaultingOrMutation(int quantity)
    {
        var (drug, rx) = await Seed(10);
        await using var context = database.Context();
        await Assert.ThrowsAsync<ArgumentException>(() => new PharmacyDispensingService(context).DispenseAsync(rx.Id.Value, quantity));
        await AssertState(drug.Id, rx.Id, 10, PrescriptionStatus.Pending);
    }

    [Fact]
    public async Task UnlinkedPrescription_CannotBeDispensedWithoutInventory()
    {
        var (drug, rx) = await Seed(10, linked: false);
        await using var context = database.Context();
        await Assert.ThrowsAsync<PharmacyDispenseConflictException>(() => new PharmacyDispensingService(context).DispenseAsync(rx.Id.Value, 1));
        await AssertState(drug.Id, rx.Id, 10, PrescriptionStatus.Pending);
    }

    [Fact]
    public async Task CancelledPrescription_DoesNotDeductStock()
    {
        var (drug, rx) = await Seed(10, cancelled: true);
        await using var context = database.Context();
        await Assert.ThrowsAsync<PharmacyDispenseConflictException>(() => new PharmacyDispensingService(context).DispenseAsync(rx.Id.Value, 3));
        await AssertState(drug.Id, rx.Id, 10, PrescriptionStatus.Cancelled);
    }

    [Fact]
    public async Task AlreadyDispensed_Is409_AndNeverDeductsTwice()
    {
        var (drug, rx) = await Seed(10);
        await using var host = await Host.Create(database.ConnectionString);
        using var first = await host.Dispense(rx.Id.Value, 3);
        using var second = await host.Dispense(rx.Id.Value, 3);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode); Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await AssertState(drug.Id, rx.Id, 7, PrescriptionStatus.Dispensed);
    }

    [Fact]
    public async Task SamePrescription_ConcurrentHttpRequests_CommitExactlyOnce()
    {
        var (drug, rx) = await Seed(10);
        await using var host = await Host.Create(database.ConnectionString, new TwoWriterBarrier());
        var responses = await Task.WhenAll(host.Dispense(rx.Id.Value, 3), host.Dispense(rx.Id.Value, 3));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();
        await AssertState(drug.Id, rx.Id, 7, PrescriptionStatus.Dispensed);
    }

    [Fact]
    public async Task SharedInventory_ConcurrentHttpRequests_CannotOversell()
    {
        var (drug, a) = await Seed(5);
        var b = Prescription.Create(a.PatientId, drug.Id, "Test Doctor", drug.Name, "Test", "Test", 7, 0, null);
        b.CreatedAtUtc = DateTime.UtcNow;
        await using (var seed = database.Context()) { seed.Prescriptions.Add(b); await seed.SaveChangesAsync(); }
        await using var host = await Host.Create(database.ConnectionString, new TwoWriterBarrier());
        var responses = await Task.WhenAll(host.Dispense(a.Id.Value, 4), host.Dispense(b.Id.Value, 4));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();
        await using var fresh = database.Context();
        Assert.Equal(1, (await fresh.Drugs.SingleAsync(d => d.Id == drug.Id)).StockQuantity);
        var rows = await fresh.Prescriptions.Where(p => p.Id == a.Id || p.Id == b.Id).ToListAsync();
        Assert.Single(rows, p => p.Status == PrescriptionStatus.Dispensed);
        Assert.Single(rows, p => p.Status == PrescriptionStatus.Pending && !p.DispensedDateUtc.HasValue);
    }

    [Fact]
    public async Task DatabaseWriteFailure_RollsBackBothRows_AndClearsTrackedMutations()
    {
        var (drug, rx) = await Seed(10);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var triggerName = "fail_dispense_" + rx.Id.Value.ToString("N");
        await using var trigger = new NpgsqlCommand($"""
            CREATE FUNCTION pharmacy.{triggerName}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
              IF NEW."Id" = '{rx.Id.Value}'::uuid THEN RAISE EXCEPTION 'isolated dispensing write failure'; END IF;
              RETURN NEW;
            END; $body$;
            CREATE TRIGGER {triggerName} BEFORE UPDATE ON pharmacy.prescriptions
            FOR EACH ROW EXECUTE FUNCTION pharmacy.{triggerName}();
            """, connection);
        await trigger.ExecuteNonQueryAsync();
        await using var context = database.Context();
        await Assert.ThrowsAsync<PharmacyDispenseStorageException>(() => new PharmacyDispensingService(context).DispenseAsync(rx.Id.Value, 3));
        Assert.Empty(context.ChangeTracker.Entries());
        await context.SaveChangesAsync();
        await AssertState(drug.Id, rx.Id, 10, PrescriptionStatus.Pending);
        await using var host = await Host.Create(database.ConnectionString);
        using var failure = await host.Dispense(rx.Id.Value, 3);
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        var body = await failure.Content.ReadAsStringAsync();
        Assert.DoesNotContain("isolated dispensing write failure", body); Assert.DoesNotContain("SQL", body);
        await AssertState(drug.Id, rx.Id, 10, PrescriptionStatus.Pending);
        await using var cleanup = new NpgsqlCommand($"DROP TRIGGER {triggerName} ON pharmacy.prescriptions; DROP FUNCTION pharmacy.{triggerName}();", connection);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Theory]
    [InlineData(Roles.Admin, 200)]
    [InlineData(Roles.Pharmacist, 200)]
    [InlineData(Roles.Doctor, 403)]
    [InlineData(Roles.Patient, 403)]
    [InlineData(null, 401)]
    public async Task DispenseAuthorization_IsUnchanged(string? role, int status)
    {
        var (drug, rx) = await Seed(10);
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Dispense(rx.Id.Value, 3, role);
        Assert.Equal(status, (int)response.StatusCode);
        await AssertState(drug.Id, rx.Id, status == 200 ? 7 : 10, status == 200 ? PrescriptionStatus.Dispensed : PrescriptionStatus.Pending);
    }

    [Theory]
    [InlineData(0, 1, 409)]
    [InlineData(10, 0, 400)]
    public async Task ApiStockAndQuantityFailures_AreControlled(int stock, int quantity, int status)
    {
        var (drug, rx) = await Seed(stock);
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Dispense(rx.Id.Value, quantity);
        Assert.Equal(status, (int)response.StatusCode);
        await AssertState(drug.Id, rx.Id, stock, PrescriptionStatus.Pending);
    }

    [Fact]
    public async Task MissingPrescription_Is404()
    {
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Dispense(Guid.NewGuid(), 3);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void CurrentEfModel_HasNoUnintendedPendingChanges()
    {
        using var context = database.Context();
        Assert.False(context.Database.HasPendingModelChanges());
    }

    // Ensures both independent transactions read the original values before either writes.
    // No timing/sleep assumptions, mock persistence or fake concurrency semantics.
    private sealed class TwoWriterBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.SetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }

    private sealed class Host(WebApplication app, HttpClient client, JwtSettings settings) : IAsyncDisposable
    {
        public static async Task<Host> Create(string connection, params IInterceptor[] interceptors)
        {
            var settings = new JwtSettings { Issuer = "PharmacyTests", Audience = "PharmacyTests",
                Secret = "isolated-pharmacy-tests-signing-key-at-least-32-characters" };
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddApplication();
            builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connection).AddInterceptors(interceptors));
            builder.Services.AddScoped<IApplicationDbContext>(p => p.GetRequiredService<ApplicationDbContext>());
            builder.Services.AddScoped<IPharmacyDispensingService, PharmacyDispensingService>();
            var sessions = new Mock<IJwtSessionValidator>();
            sessions.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, string? version, CancellationToken _) => JwtSessionVersion.Matches(version, "pharmacy-test-stamp"));
            builder.Services.AddSingleton(sessions.Object);
            builder.Services.AddAccountJwtAuthentication(settings);
            builder.Services.AddAuthorization(options => {
                options.AddPolicy(Policies.CanDispenseMedication, p => p.RequireRole(Roles.Admin, Roles.Pharmacist));
                options.AddPolicy(Policies.CanViewPatients, p => p.RequireRole(Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist));
                options.AddPolicy(Policies.CanPrescribeMedication, p => p.RequireRole(Roles.Admin, Roles.Doctor));
            });
            var app = builder.Build();
            app.UseMiddleware<GlobalExceptionHandlingMiddleware>(); app.UseAuthentication(); app.UseAuthorization();
            app.MapPharmacyEndpoints(); await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new HttpClient { BaseAddress = new Uri(address) }, settings);
        }

        public async Task<HttpResponseMessage> Dispense(Guid id, int quantity, string? role = Roles.Pharmacist)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/pharmacy/prescriptions/{id}/dispense")
                { Content = JsonContent.Create(new { quantityToDeduct = quantity }) };
            if (role is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtTokenGenerator(Options.Create(settings))
                    .GenerateToken(Guid.NewGuid().ToString(), "test@example.invalid", "Test", "User", [role], "pharmacy-test-stamp"));
            return await client.SendAsync(request);
        }

        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
