using System.Data;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Pharmacy;
using HealthERP.Application.Pharmacy.Models;
using HealthERP.Domain.Pharmacy;
using HealthERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HealthERP.Infrastructure.Services;

public sealed class PharmacyDispensingService(ApplicationDbContext context) : IPharmacyDispensingService
{
    public async Task<PrescriptionDto> DispenseAsync(Guid prescriptionId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) throw new ArgumentException("Dispense quantity must be positive.", nameof(quantity));
        // Begin before reading: PostgreSQL must detect competing reads/writes of both rows.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var id = new PrescriptionId(prescriptionId);
            var prescription = await context.Prescriptions.Include(p => p.Patient).Include(p => p.Drug)
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Prescription was not found.");
            if (prescription.Status != PrescriptionStatus.Pending)
                throw new PharmacyDispenseConflictException("Only a pending prescription can be dispensed. Refresh the list before retrying.");
            if (prescription.Drug is null)
                throw new PharmacyDispenseConflictException("This prescription is not linked to available inventory and cannot be dispensed.");
            if (prescription.Drug.StockQuantity < quantity)
                throw new PharmacyDispenseConflictException("Insufficient inventory for the requested dispense quantity.");

            prescription.Drug.DeductStock(quantity);
            prescription.Dispense();
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PrescriptionDto(prescription.Id.Value, prescription.PatientId.Value,
                prescription.Patient is null ? "Unknown Patient" : $"{prescription.Patient.FirstName} {prescription.Patient.LastName}",
                prescription.DrugId?.Value, prescription.Drug.Name, prescription.DoctorName, prescription.MedicationName,
                prescription.Dosage, prescription.Frequency, prescription.DurationDays, prescription.Refills,
                prescription.Instructions, prescription.Status.ToString(), prescription.PrescribedDateUtc, prescription.DispensedDateUtc);
        }
        catch (Exception exception)
        {
            // Cancellation must not prevent rollback. Detach failed mutations so a later save cannot persist them.
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch { /* Preserve the original failure if the connection also failed during rollback. */ }
            finally { context.ChangeTracker.Clear(); }
            if (exception is DbUpdateConcurrencyException || IsPostgresConflict(exception))
                throw new PharmacyDispenseConflictException("Inventory or prescription changed during dispensing. Refresh and retry.");
            if (exception is DbUpdateException or NpgsqlException)
                throw new PharmacyDispenseStorageException(exception);
            throw;
        }
    }

    private static bool IsPostgresConflict(Exception exception) =>
        exception is PostgresException { SqlState: "40001" or "40P01" }
        || exception.InnerException is PostgresException { SqlState: "40001" or "40P01" };
}
