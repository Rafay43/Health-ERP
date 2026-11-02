using System.Data;
using HealthERP.Application.Billing;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Billing;
using HealthERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HealthERP.Infrastructure.Services;

public sealed class BillingPaymentService(ApplicationDbContext context) : IBillingPaymentService
{
    public async Task RecordAsync(Guid invoiceId, decimal amount, PaymentMethod method,
        string? referenceNumber = null, string? notes = null, CancellationToken cancellationToken = default)
    {
        Payment.ValidateInput(amount, method, referenceNumber, notes);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // Lock before validating the balance. A waiter reads the preceding committed payment,
            // so two valid partial payments both succeed without last-write-wins or automatic retries.
            var invoice = await context.Invoices
                .FromSqlInterpolated($"SELECT * FROM billing.invoices WHERE \"Id\" = {invoiceId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Invoice was not found.");

            var tracked = context.ChangeTracker.Entries<Invoice>().SingleOrDefault(e => e.Entity.Id == invoice.Id);
            if (tracked is null) context.Invoices.Attach(invoice);
            else
            {
                // Even a reused scope must not validate against an earlier tracked balance.
                await tracked.ReloadAsync(cancellationToken);
                invoice = tracked.Entity;
            }

            Payment payment;
            try { payment = invoice.RecordPayment(amount, method, referenceNumber, notes); }
            catch (InvalidOperationException exception) { throw new BillingPaymentConflictException(exception.Message); }
            context.Set<Payment>().Add(payment);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch { /* Preserve the original failure if the connection also failed during rollback. */ }
            finally { context.ChangeTracker.Clear(); }
            if (ExceptionChain(exception).Any(e => e is DbUpdateConcurrencyException
                or PostgresException { SqlState: "40001" or "40P01" }))
                throw new BillingPaymentConflictException("The invoice changed during payment. Refresh the invoice before retrying.");
            if (ExceptionChain(exception).Any(e => e is DbUpdateException or NpgsqlException))
                throw new BillingPaymentStorageException(exception);
            throw;
        }
    }

    // Npgsql's execution strategy can wrap DbUpdateException in InvalidOperationException.
    private static IEnumerable<Exception> ExceptionChain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            yield return current;
    }
}
