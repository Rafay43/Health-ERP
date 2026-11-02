using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Patients;
using HealthERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HealthERP.Api.IntegrationTests;

[Collection("Pharmacy PostgreSQL")]
public class InpatientOriginalRaceTests(PharmacyPostgresFixture database)
{
    private async Task<(Patient PatientA, Patient PatientB, Bed BedA, Bed BedB, Bed Destination)> Seed()
    {
        var a = Patient.Register("I-" + Guid.NewGuid().ToString("N")[..16], "Original", "A", new DateOnly(1990, 1, 1));
        var b = Patient.Register("I-" + Guid.NewGuid().ToString("N")[..16], "Original", "B", new DateOnly(1990, 1, 1));
        Bed NewBed() => Bed.Create("Original-" + Guid.NewGuid().ToString("N"), "1", "1", "General", 100);
        var ba = NewBed(); var bb = NewBed(); var bx = NewBed();
        await using var context = database.Context();
        context.Patients.AddRange(a, b); context.Beds.AddRange(ba, bb, bx); await context.SaveChangesAsync();
        return (a, b, ba, bb, bx);
    }

    [Fact]
    public async Task FormerAdmissionWorkflow_AllowsTwoActiveAdmissionsOnOneBed()
    {
        var (a, b, _, _, x) = await Seed();
        var barrier = new TwoSaveBarrier();
        async Task FormerAdmit(PatientId patientId)
        {
            await using var context = database.Context(barrier);
            // Current pre-Batch-5 handler algorithm, preserved only for disposable reproduction.
            Assert.True(await context.Patients.AnyAsync(p => p.Id == patientId));
            Assert.False(await context.Admissions.AnyAsync(p => p.PatientId == patientId && !p.IsDischarged));
            var bed = await context.Beds.SingleAsync(b => b.Id == x.Id);
            bed.Occupy();
            context.Admissions.Add(Admission.Create(patientId, bed.Id, "Test Doctor", "Test", bed.DailyRate));
            await context.SaveChangesAsync();
        }
        await Task.WhenAll(FormerAdmit(a.Id), FormerAdmit(b.Id));
        await using var fresh = database.Context();
        Assert.Equal(2, await fresh.Admissions.CountAsync(a => a.BedId == x.Id && !a.IsDischarged));
        Assert.Equal(BedStatus.Occupied, (await fresh.Beds.SingleAsync(b => b.Id == x.Id)).Status);
    }

    [Fact]
    public async Task FormerTransferWorkflow_AllowsTwoActiveAdmissionsOnOneDestination()
    {
        var (a, b, ba, bb, x) = await Seed();
        var aa = Admission.Create(a.Id, ba.Id, "Test Doctor", "Test", ba.DailyRate);
        var ab = Admission.Create(b.Id, bb.Id, "Test Doctor", "Test", bb.DailyRate);
        ba.Occupy(); bb.Occupy();
        await using (var seed = database.Context())
        {
            seed.Beds.UpdateRange(ba, bb); seed.Admissions.AddRange(aa, ab); await seed.SaveChangesAsync();
        }
        var barrier = new TwoSaveBarrier();
        async Task FormerTransfer(AdmissionId id)
        {
            await using var context = database.Context(barrier);
            var admission = await context.Admissions.Include(a => a.Bed).SingleAsync(a => a.Id == id);
            var destination = await context.Beds.SingleAsync(b => b.Id == x.Id);
            Assert.False(admission.IsDischarged); Assert.NotEqual(BedStatus.Occupied, destination.Status);
            admission.Bed!.Release(); destination.Occupy(); admission.TransferBed(destination.Id, destination.DailyRate, "Test");
            await context.SaveChangesAsync();
        }
        await Task.WhenAll(FormerTransfer(aa.Id), FormerTransfer(ab.Id));
        await using var fresh = database.Context();
        Assert.Equal(2, await fresh.Admissions.CountAsync(a => a.BedId == x.Id && !a.IsDischarged));
        Assert.Equal(BedStatus.Available, (await fresh.Beds.SingleAsync(b => b.Id == ba.Id)).Status);
        Assert.Equal(BedStatus.Available, (await fresh.Beds.SingleAsync(b => b.Id == bb.Id)).Status);
    }

    private sealed class TwoSaveBarrier : SaveChangesInterceptor
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
}
