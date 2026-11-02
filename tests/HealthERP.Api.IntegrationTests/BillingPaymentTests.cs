using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HealthERP.Api.Authentication;
using HealthERP.Api.Endpoints;
using HealthERP.Api.Middleware;
using HealthERP.Application;
using HealthERP.Application.Billing;
using HealthERP.Application.Common.Configurations;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Security;
using HealthERP.Domain.Billing;
using HealthERP.Domain.Patients;
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

// Reuse Batch 3's guarded disposable PostgreSQL fixture, without changing it.
[Collection("Pharmacy PostgreSQL")]
public class BillingPaymentTests(PharmacyPostgresFixture database)
{
    private async Task<Invoice> Seed(decimal paid = 0, bool cancelled = false)
    {
        await using var context = database.Context();
        var patient = Patient.Register("B-" + Guid.NewGuid().ToString("N")[..16], "Billing", "Test", new DateOnly(1990, 1, 1));
        patient.CreatedAtUtc = DateTime.UtcNow;
        var invoice = Invoice.Create(patient.Id, "B-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddDays(30));
        invoice.CreatedAtUtc = DateTime.UtcNow;
        invoice.AddLineItem("Test consultation", 1, 100m);
        if (paid > 0) invoice.RecordPayment(paid, PaymentMethod.Cash);
        if (cancelled) invoice.Cancel();
        context.Patients.Add(patient); context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        return invoice;
    }

    private async Task AssertState(InvoiceId id, decimal paid, int count, InvoiceStatus status)
    {
        await using var fresh = database.Context();
        var invoice = await fresh.Invoices.Include(i => i.Payments).SingleAsync(i => i.Id == id);
        Assert.Equal(100m, invoice.TotalAmount);
        Assert.Equal(paid, invoice.PaidAmount);
        Assert.Equal(100m - paid, invoice.BalanceDue);
        Assert.Equal(status, invoice.Status);
        Assert.Equal(count, invoice.Payments.Count);
        Assert.Equal(paid, invoice.Payments.Sum(p => p.Amount));
        Assert.All(invoice.Payments, p => Assert.True(p.Amount > 0));
    }

    [Theory]
    [InlineData(0, 100, 100, 1, InvoiceStatus.Paid)]
    [InlineData(0, 30, 30, 1, InvoiceStatus.PartiallyPaid)]
    [InlineData(30, 20, 50, 2, InvoiceStatus.PartiallyPaid)]
    public async Task SuccessfulPayment_PersistsMatchingAmountHistoryBalanceAndStatus(int before, int amount, int after, int count, InvoiceStatus status)
    {
        var invoice = await Seed(before);
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Pay(invoice.Id.Value, amount);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertState(invoice.Id, after, count, status);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("30.005")]
    [InlineData("10000000000000000")]
    public async Task InvalidAmount_Is400_WithoutRoundingOrMutation(string input)
    {
        var invoice = await Seed();
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Pay(invoice.Id.Value, decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertState(invoice.Id, 0, 0, InvoiceStatus.Pending);
    }

    [Fact]
    public async Task ExactCents_AreNotRoundedOrReplaced()
    {
        var invoice = await Seed();
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Pay(invoice.Id.Value, 0.01m);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertState(invoice.Id, 0.01m, 1, InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task FormerUnlockedWorkflow_ReproducesLostUpdateOnPostgreSql()
    {
        var seeded = await Seed();
        var barrier = new FormerSaveBarrier();
        async Task FormerPayment(decimal amount)
        {
            await using var context = database.Context(barrier);
            // Exact former handler algorithm, isolated here to verify the reported root cause.
            var invoice = await context.Invoices.Include(i => i.Payments).SingleAsync(i => i.Id == seeded.Id);
            invoice.RecordPayment(amount, PaymentMethod.Cash);
            await context.SaveChangesAsync();
        }
        await Task.WhenAll(FormerPayment(30), FormerPayment(40));
        await using var fresh = database.Context();
        var broken = await fresh.Invoices.Include(i => i.Payments).SingleAsync(i => i.Id == seeded.Id);
        Assert.Equal(2, broken.Payments.Count);
        Assert.Equal(70m, broken.Payments.Sum(p => p.Amount));
        Assert.Contains(broken.PaidAmount, new[] { 30m, 40m });
        Assert.NotEqual(broken.Payments.Sum(p => p.Amount), broken.PaidAmount);
    }

    [Theory]
    [InlineData(80, 21)]
    [InlineData(100, 1)]
    public async Task OverpaidOrFullyPaidAttempt_Is409_AndLeavesHistoryUnchanged(int before, int amount)
    {
        var invoice = await Seed(before);
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Pay(invoice.Id.Value, amount);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertState(invoice.Id, before, 1, before == 100 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task CancelledInvoice_Is409_WithoutMutation()
    {
        var invoice = await Seed(cancelled: true);
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Pay(invoice.Id.Value, 10);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertState(invoice.Id, 0, 0, InvoiceStatus.Cancelled);
    }

    [Fact]
    public async Task MissingInvoice_Is404()
    {
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Pay(Guid.NewGuid(), 10);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 30, 40, 70, 2, 2, InvoiceStatus.PartiallyPaid)]
    [InlineData(80, 15, 15, 95, 2, 1, InvoiceStatus.PartiallyPaid)]
    [InlineData(90, 10, 10, 100, 2, 1, InvoiceStatus.Paid)]
    public async Task ConcurrentIndependentHttpRequests_PreserveEveryFinancialInvariant(
        int before, int a, int b, int after, int totalCount, int successes, InvoiceStatus status)
    {
        var invoice = await Seed(before);
        await using var host = await Host.Create(database.ConnectionString);
        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync();
        await using var hold = await blocker.BeginTransactionAsync();
        await using var rowLock = new NpgsqlCommand("SELECT 1 FROM billing.invoices WHERE \"Id\" = @id FOR UPDATE", blocker, hold);
        rowLock.Parameters.AddWithValue("id", invoice.Id.Value);
        await rowLock.ExecuteScalarAsync();

        var first = host.Pay(invoice.Id.Value, a);
        var second = host.Pay(invoice.Id.Value, b);
        try
        {
            // Both independent request transactions must actually wait in PostgreSQL before release.
            // The former unlocked-read flow would instead wait at UPDATE with stale amounts,
            // then fail the persisted-state assertions below after this same release.
            await using var observer = new NpgsqlConnection(database.ConnectionString);
            await observer.OpenAsync();
            await using var waiting = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                AND query ILIKE '%invoices%' AND pid <> pg_backend_pid()
                """, observer);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (Convert.ToInt32(await waiting.ExecuteScalarAsync(timeout.Token)) < 2)
                await Task.Delay(25, timeout.Token);
        }
        finally { await hold.RollbackAsync(); } // Release even if observation fails.

        var responses = await Task.WhenAll(first, second);
        Assert.Equal(successes, responses.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(2 - successes, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();
        await AssertState(invoice.Id, after, totalCount, status);
    }

    [Theory]
    [InlineData(false, "P0001", 500)]
    [InlineData(true, "P0001", 500)]
    [InlineData(false, "40001", 409)]
    [InlineData(false, "40P01", 409)]
    public async Task PostgreSqlWriteFailure_RollsBackInvoiceAndPayment_AndHidesInternals(bool failInvoice, string sqlState, int expected)
    {
        var invoice = await Seed(30);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var name = "fail_billing_" + invoice.Id.Value.ToString("N");
        var table = failInvoice ? "invoices" : "payments";
        var operation = failInvoice ? "UPDATE" : "INSERT";
        var column = failInvoice ? "Id" : "InvoiceId";
        // Identifiers/UUID come exclusively from this disposable test, never external input.
        await using var trigger = new NpgsqlCommand($"""
            CREATE FUNCTION billing.{name}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
              IF NEW."{column}" = '{invoice.Id.Value}'::uuid THEN
                RAISE EXCEPTION 'isolated billing write failure' USING ERRCODE = '{sqlState}';
              END IF;
              RETURN NEW;
            END; $body$;
            CREATE TRIGGER {name} BEFORE {operation} ON billing.{table}
            FOR EACH ROW EXECUTE FUNCTION billing.{name}();
            """, connection);
        await trigger.ExecuteNonQueryAsync();
        try
        {
            await using var context = database.Context();
            var service = new BillingPaymentService(context);
            if (expected == 409)
                await Assert.ThrowsAsync<BillingPaymentConflictException>(() => service.RecordAsync(invoice.Id.Value, 20, PaymentMethod.Cash));
            else
                await Assert.ThrowsAsync<BillingPaymentStorageException>(() => service.RecordAsync(invoice.Id.Value, 20, PaymentMethod.Cash));
            Assert.Empty(context.ChangeTracker.Entries());
            await context.SaveChangesAsync(); // Failed tracked mutations cannot leak later.
            await AssertState(invoice.Id, 30, 1, InvoiceStatus.PartiallyPaid);
            await using var host = await Host.Create(database.ConnectionString);
            using var response = await host.Pay(invoice.Id.Value, 20);
            Assert.Equal(expected, (int)response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("isolated billing write failure", body);
            Assert.DoesNotContain(name, body);
            await AssertState(invoice.Id, 30, 1, InvoiceStatus.PartiallyPaid);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP TRIGGER {name} ON billing.{table}; DROP FUNCTION billing.{name}();", connection);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task CancellationAfterSave_BeforeCommit_RollsBackBothWrites()
    {
        var invoice = await Seed();
        await using var context = database.Context(new CancelAfterSave());
        await Assert.ThrowsAsync<OperationCanceledException>(() => new BillingPaymentService(context).RecordAsync(invoice.Id.Value, 30, PaymentMethod.Cash));
        Assert.Empty(context.ChangeTracker.Entries());
        await AssertState(invoice.Id, 0, 0, InvoiceStatus.Pending);
    }

    [Fact]
    public async Task EarlierTrackedInvoice_DoesNotOverrideNewAuthoritativeBalance()
    {
        var invoice = await Seed();
        await using var stale = database.Context();
        await stale.Invoices.SingleAsync(i => i.Id == invoice.Id);
        await using (var other = database.Context())
            await new BillingPaymentService(other).RecordAsync(invoice.Id.Value, 80, PaymentMethod.Cash);
        await Assert.ThrowsAsync<BillingPaymentConflictException>(() => new BillingPaymentService(stale).RecordAsync(invoice.Id.Value, 21, PaymentMethod.Cash));
        await AssertState(invoice.Id, 80, 1, InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task OptionalReferences_AreNotInventedAsUniqueIdempotencyKeys()
    {
        var invoice = await Seed();
        await using var host = await Host.Create(database.ConnectionString);
        using var a = await host.Pay(invoice.Id.Value, 30, reference: "manual receipt");
        using var b = await host.Pay(invoice.Id.Value, 30, reference: "manual receipt");
        Assert.Equal(HttpStatusCode.NoContent, a.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, b.StatusCode);
        await AssertState(invoice.Id, 60, 2, InvoiceStatus.PartiallyPaid);
    }

    [Theory]
    [InlineData(Roles.Admin, 204)]
    [InlineData(Roles.BillingStaff, 204)]
    [InlineData(Roles.Doctor, 403)]
    [InlineData(Roles.Patient, 403)]
    [InlineData(null, 401)]
    public async Task PaymentAuthorization_RemainsServerEnforced(string? role, int status)
    {
        var invoice = await Seed();
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Pay(invoice.Id.Value, 30, role);
        Assert.Equal(status, (int)response.StatusCode);
        await AssertState(invoice.Id, status == 204 ? 30 : 0, status == 204 ? 1 : 0,
            status == 204 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Pending);
    }

    [Fact]
    public void CurrentModel_RequiresNoMigration()
    {
        using var context = database.Context();
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private sealed class CancelAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => throw new OperationCanceledException("Test cancellation before commit.");
    }

    private sealed class FormerSaveBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.SetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

    private sealed class Host(WebApplication app, HttpClient client, JwtSettings settings) : IAsyncDisposable
    {
        public static async Task<Host> Create(string connection)
        {
            var settings = new JwtSettings { Issuer = "BillingTests", Audience = "BillingTests",
                Secret = "isolated-billing-tests-signing-key-at-least-32-characters" };
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddApplication();
            builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connection));
            builder.Services.AddScoped<IApplicationDbContext>(p => p.GetRequiredService<ApplicationDbContext>());
            builder.Services.AddScoped<IBillingPaymentService, BillingPaymentService>();
            var sessions = new Mock<IJwtSessionValidator>();
            sessions.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, string? version, CancellationToken _) => JwtSessionVersion.Matches(version, "billing-test-stamp"));
            builder.Services.AddSingleton(sessions.Object);
            builder.Services.AddAccountJwtAuthentication(settings);
            builder.Services.AddAuthorization(options => options.AddPolicy(Policies.CanManageBilling,
                p => p.RequireRole(Roles.Admin, Roles.BillingStaff)));
            var app = builder.Build();
            app.UseMiddleware<GlobalExceptionHandlingMiddleware>(); app.UseAuthentication(); app.UseAuthorization();
            app.MapBillingEndpoints(); await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new HttpClient { BaseAddress = new Uri(address) }, settings);
        }

        public async Task<HttpResponseMessage> Pay(Guid id, decimal amount, string? role = Roles.BillingStaff, string? reference = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{id}/payments")
                { Content = JsonContent.Create(new { amount, paymentMethod = 1, referenceNumber = reference, notes = (string?)null }) };
            if (role is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtTokenGenerator(Options.Create(settings))
                    .GenerateToken(Guid.NewGuid().ToString(), "test@example.invalid", "Test", "User", [role], "billing-test-stamp"));
            return await client.SendAsync(request);
        }

        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
