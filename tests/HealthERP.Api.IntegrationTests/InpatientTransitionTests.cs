using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Data.Common;
using HealthERP.Api.Authentication;
using HealthERP.Api.Endpoints;
using HealthERP.Api.Middleware;
using HealthERP.Application;
using HealthERP.Application.Common.Configurations;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Security;
using HealthERP.Application.Inpatient;
using HealthERP.Application.Inpatient.Queries;
using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Triage;
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

[Collection("Pharmacy PostgreSQL")]
public class InpatientTransitionTests(PharmacyPostgresFixture database)
{
    private sealed record Setup(Patient A, Patient B, Bed SourceA, Bed SourceB, Bed Destination, Admission? AdmissionA, Admission? AdmissionB);

    private async Task<Setup> Seed(bool admitA = false, bool admitB = false)
    {
        Patient NewPatient(string name) => Patient.Register("I-" + Guid.NewGuid().ToString("N")[..16], "Test", name, new DateOnly(1990, 1, 1));
        Bed NewBed() => Bed.Create("Ward-" + Guid.NewGuid().ToString("N"), "1", "1", "General", 100);
        var a = NewPatient("A"); var b = NewPatient("B"); var ba = NewBed(); var bb = NewBed(); var x = NewBed();
        Admission? aa = admitA ? Admission.Create(a.Id, ba.Id, "Test Doctor", "Test", ba.DailyRate) : null;
        Admission? ab = admitB ? Admission.Create(b.Id, bb.Id, "Test Doctor", "Test", bb.DailyRate) : null;
        if (admitA) ba.Occupy(); if (admitB) bb.Occupy();
        await using var context = database.Context();
        context.Patients.AddRange(a, b); context.Beds.AddRange(ba, bb, x);
        if (aa is not null) context.Admissions.Add(aa); if (ab is not null) context.Admissions.Add(ab);
        await context.SaveChangesAsync(); return new(a, b, ba, bb, x, aa, ab);
    }

    private async Task AssertConsistency(Setup s)
    {
        await using var fresh = database.Context();
        foreach (var bed in new[] { s.SourceA, s.SourceB, s.Destination })
        {
            var status = (await fresh.Beds.SingleAsync(b => b.Id == bed.Id)).Status;
            var owners = await fresh.Admissions.CountAsync(a => a.BedId == bed.Id && !a.IsDischarged);
            Assert.InRange(owners, 0, 1);
            Assert.Equal(status == BedStatus.Occupied ? 1 : 0, owners);
        }
        foreach (var patient in new[] { s.A, s.B })
            Assert.InRange(await fresh.Admissions.CountAsync(a => a.PatientId == patient.Id && !a.IsDischarged), 0, 1);
    }

    [Fact]
    public async Task Admission_CreatesActiveOwnerAndOccupiesBed()
    {
        var s = await Seed(); await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Admit(s.A.Id.Value, s.Destination.Id.Value);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var fresh = database.Context();
        var admission = await fresh.Admissions.SingleAsync(a => a.PatientId == s.A.Id);
        Assert.Equal(s.Destination.Id, admission.BedId); Assert.False(admission.IsDischarged);
        Assert.Null(admission.DischargeDateUtc); Assert.Equal(100m, admission.DailyRateSnapshot);
        await AssertConsistency(s);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OccupiedOrMaintenanceBed_RejectsAdmissionWithoutPartialCreation(bool maintenance)
    {
        var s = await Seed(admitA: !maintenance);
        if (maintenance)
        {
            await using var mark = database.Context(); var bed = await mark.Beds.SingleAsync(b => b.Id == s.SourceA.Id);
            bed.MarkMaintenance(); await mark.SaveChangesAsync();
        }
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Admit(s.B.Id.Value, s.SourceA.Id.Value);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var fresh = database.Context(); Assert.False(await fresh.Admissions.AnyAsync(a => a.PatientId == s.B.Id));
        await AssertConsistency(s);
    }

    [Fact]
    public async Task ExistingActivePatient_CannotAcquireASecondBed()
    {
        var s = await Seed(admitA: true); await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Admit(s.A.Id.Value, s.Destination.Id.Value);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); await AssertConsistency(s);
    }

    [Fact]
    public async Task AvailableFlagWithExistingOwner_DoesNotAllowSecondAdmission()
    {
        var s = await Seed(admitA: true);
        await using (var inconsistent = database.Context())
        {
            var bed = await inconsistent.Beds.SingleAsync(b => b.Id == s.SourceA.Id); bed.Release(); await inconsistent.SaveChangesAsync();
        }
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Admit(s.B.Id.Value, s.SourceA.Id.Value);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var fresh = database.Context();
        Assert.Equal(1, await fresh.Admissions.CountAsync(a => a.BedId == s.SourceA.Id && !a.IsDischarged));
        Assert.Equal(BedStatus.Available, (await fresh.Beds.SingleAsync(b => b.Id == s.SourceA.Id)).Status);
        Assert.False(await fresh.Admissions.AnyAsync(a => a.PatientId == s.B.Id)); // No automatic historical repair.
    }

    [Fact]
    public async Task Transfer_ReleasesSourceAcquiresDestinationAndUpdatesAdmission()
    {
        var s = await Seed(admitA: true); await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Transfer(s.AdmissionA!.Id.Value, s.Destination.Id.Value);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var fresh = database.Context(); var admission = await fresh.Admissions.SingleAsync(a => a.Id == s.AdmissionA.Id);
        Assert.Equal(s.Destination.Id, admission.BedId); Assert.False(admission.IsDischarged);
        Assert.Equal(BedStatus.Available, (await fresh.Beds.SingleAsync(b => b.Id == s.SourceA.Id)).Status);
        await AssertConsistency(s);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransferToOccupiedOrMaintenanceBed_PreservesSourceAndAdmission(bool maintenance)
    {
        var s = await Seed(admitA: true, admitB: !maintenance);
        if (maintenance)
        {
            await using var mark = database.Context(); var bed = await mark.Beds.SingleAsync(b => b.Id == s.SourceB.Id);
            bed.MarkMaintenance(); await mark.SaveChangesAsync();
        }
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Transfer(s.AdmissionA!.Id.Value, s.SourceB.Id.Value);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var fresh = database.Context(); Assert.Equal(s.SourceA.Id, (await fresh.Admissions.SingleAsync(a => a.Id == s.AdmissionA.Id)).BedId);
        await AssertConsistency(s);
    }

    [Fact]
    public async Task Discharge_ReleasesBedAndRejectsRepeatWithoutReleasingItsNewOwner()
    {
        var s = await Seed(admitA: true); await using var host = await Host.Create(database.ConnectionString);
        using var first = await host.Discharge(s.AdmissionA!.Id.Value); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertConsistency(s);
        using var next = await host.Admit(s.B.Id.Value, s.SourceA.Id.Value); Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        using var repeat = await host.Discharge(s.AdmissionA.Id.Value); Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        using var transfer = await host.Transfer(s.AdmissionA.Id.Value, s.Destination.Id.Value); Assert.Equal(HttpStatusCode.Conflict, transfer.StatusCode);
        await using var fresh = database.Context(); var a = await fresh.Admissions.SingleAsync(a => a.Id == s.AdmissionA.Id);
        Assert.True(a.IsDischarged); Assert.NotNull(a.DischargeDateUtc); Assert.Equal("Test discharge", a.DischargeSummary);
        Assert.True(a.TotalRoomCharge >= 100m); await AssertConsistency(s);
    }

    [Fact]
    public async Task SameBedTransfer_IsControlledConflict()
    {
        var s = await Seed(admitA: true); await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Transfer(s.AdmissionA!.Id.Value, s.SourceA.Id.Value);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); await AssertConsistency(s);
    }

    [Theory]
    [InlineData("patient")]
    [InlineData("bed")]
    [InlineData("transfer")]
    [InlineData("discharge")]
    [InlineData("destination")]
    public async Task MissingEntity_Is404_WithoutMutation(string missing)
    {
        var s = await Seed(admitA: true); await using var host = await Host.Create(database.ConnectionString);
        using var response = missing switch {
            "patient" => await host.Admit(Guid.NewGuid(), s.Destination.Id.Value),
            "bed" => await host.Admit(s.B.Id.Value, Guid.NewGuid()),
            "destination" => await host.Transfer(s.AdmissionA!.Id.Value, Guid.NewGuid()),
            "transfer" => await host.Transfer(Guid.NewGuid(), s.Destination.Id.Value),
            _ => await host.Discharge(Guid.NewGuid()) };
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); await AssertConsistency(s);
    }

    [Fact]
    public async Task InvalidDoctor_Is400_WithoutAcquiringBed()
    {
        var s = await Seed(); await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Send("/api/v1/inpatient/admissions", new { patientId = s.A.Id.Value, bedId = s.Destination.Id.Value,
            admittingDoctorName = "", chiefComplaint = "Test" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var fresh = database.Context(); Assert.False(await fresh.Admissions.AnyAsync(a => a.PatientId == s.A.Id));
        await AssertConsistency(s);
    }

    [Theory]
    [InlineData("admit")]
    [InlineData("transfer")]
    [InlineData("mixed")]
    [InlineData("double-transfer")]
    [InlineData("transfer-discharge")]
    [InlineData("double-discharge")]
    [InlineData("same-patient")]
    [InlineData("emergency-bed")]
    [InlineData("emergency-patient")]
    public async Task ConcurrentHttpTransitions_CommitOneCoherentWinner(string race)
    {
        var s = await Seed(admitA: race is "transfer" or "mixed" or "double-transfer" or "transfer-discharge" or "double-discharge",
            admitB: race == "transfer");
        TriageRecord? triage = null;
        if (race.StartsWith("emergency")) triage = await SeedTriage(s.A.Id);
        await using var host = await Host.Create(database.ConnectionString, new TwoSaveBarrier());
        Task<HttpResponseMessage> first; Task<HttpResponseMessage> second;
        switch (race)
        {
            case "admit": first = host.Admit(s.A.Id.Value, s.Destination.Id.Value); second = host.Admit(s.B.Id.Value, s.Destination.Id.Value); break;
            case "transfer": first = host.Transfer(s.AdmissionA!.Id.Value, s.Destination.Id.Value); second = host.Transfer(s.AdmissionB!.Id.Value, s.Destination.Id.Value); break;
            case "mixed": first = host.Transfer(s.AdmissionA!.Id.Value, s.Destination.Id.Value); second = host.Admit(s.B.Id.Value, s.Destination.Id.Value); break;
            case "double-transfer": first = host.Transfer(s.AdmissionA!.Id.Value, s.Destination.Id.Value); second = host.Transfer(s.AdmissionA.Id.Value, s.SourceB.Id.Value); break;
            case "transfer-discharge": first = host.Transfer(s.AdmissionA!.Id.Value, s.Destination.Id.Value); second = host.Discharge(s.AdmissionA.Id.Value); break;
            case "double-discharge": first = host.Discharge(s.AdmissionA!.Id.Value); second = host.Discharge(s.AdmissionA.Id.Value); break;
            case "same-patient": first = host.Admit(s.A.Id.Value, s.SourceA.Id.Value); second = host.Admit(s.A.Id.Value, s.SourceB.Id.Value); break;
            case "emergency-bed": first = host.Emergency(triage!.Id.Value, s.Destination.Id.Value); second = host.Admit(s.B.Id.Value, s.Destination.Id.Value); break;
            default: first = host.Emergency(triage!.Id.Value, s.Destination.Id.Value); second = host.Admit(s.A.Id.Value, s.SourceB.Id.Value); break;
        }
        var responses = await Task.WhenAll(first, second);
        Assert.Single(responses, r => r.IsSuccessStatusCode); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var firstWon = responses[0].IsSuccessStatusCode;
        foreach (var response in responses) response.Dispose();
        await AssertConsistency(s);
        await using var fresh = database.Context();
        if (race is "admit" or "mixed" or "transfer" or "emergency-bed")
            Assert.Equal(1, await fresh.Admissions.CountAsync(a => a.BedId == s.Destination.Id && !a.IsDischarged));
        if (race == "transfer")
        {
            var loser = firstWon ? s.AdmissionB! : s.AdmissionA!;
            var original = firstWon ? s.SourceB : s.SourceA;
            Assert.Equal(original.Id, (await fresh.Admissions.SingleAsync(a => a.Id == loser.Id)).BedId);
            Assert.Equal(BedStatus.Occupied, (await fresh.Beds.SingleAsync(b => b.Id == original.Id)).Status);
        }
        if (race == "mixed" && !firstWon)
            Assert.Equal(s.SourceA.Id, (await fresh.Admissions.SingleAsync(a => a.Id == s.AdmissionA!.Id)).BedId);
        if (race == "double-transfer")
        {
            var winner = firstWon ? s.Destination : s.SourceB; var loser = firstWon ? s.SourceB : s.Destination;
            Assert.Equal(winner.Id, (await fresh.Admissions.SingleAsync(a => a.Id == s.AdmissionA!.Id)).BedId);
            Assert.Equal(BedStatus.Available, (await fresh.Beds.SingleAsync(b => b.Id == loser.Id)).Status);
            Assert.Equal(BedStatus.Available, (await fresh.Beds.SingleAsync(b => b.Id == s.SourceA.Id)).Status);
        }
        if (race == "transfer-discharge")
        {
            var admission = await fresh.Admissions.SingleAsync(a => a.Id == s.AdmissionA!.Id);
            Assert.Equal(!firstWon, admission.IsDischarged);
            Assert.Equal(firstWon ? s.Destination.Id : s.SourceA.Id, admission.BedId);
            Assert.Equal(BedStatus.Available, (await fresh.Beds.SingleAsync(b => b.Id == s.SourceA.Id)).Status);
        }
        if (race == "double-discharge") Assert.True((await fresh.Admissions.SingleAsync(a => a.Id == s.AdmissionA!.Id)).IsDischarged);
        if (race is "same-patient" or "emergency-patient")
            Assert.Equal(1, await fresh.Admissions.CountAsync(a => a.PatientId == s.A.Id && !a.IsDischarged));
        if (triage is not null)
            Assert.Equal(firstWon ? TriageStatus.TransferredToInpatient : TriageStatus.Triaged,
                (await fresh.TriageRecords.SingleAsync(t => t.Id == triage.Id)).Status);
    }

    [Fact]
    public async Task CrossTransfersToOccupiedBeds_RejectBothWithoutDeadlockOrMutation()
    {
        var s = await Seed(admitA: true, admitB: true); await using var host = await Host.Create(database.ConnectionString);
        var responses = await Task.WhenAll(host.Transfer(s.AdmissionA!.Id.Value, s.SourceB.Id.Value), host.Transfer(s.AdmissionB!.Id.Value, s.SourceA.Id.Value));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        foreach (var response in responses) response.Dispose(); await AssertConsistency(s);
    }

    [Theory]
    [InlineData("admit", false, "P0001", 500)]
    [InlineData("admit", true, "P0001", 500)]
    [InlineData("transfer", false, "P0001", 500)]
    [InlineData("transfer", true, "P0001", 500)]
    [InlineData("discharge", false, "P0001", 500)]
    [InlineData("discharge", true, "P0001", 500)]
    [InlineData("admit", false, "40001", 409)]
    [InlineData("admit", false, "40P01", 409)]
    public async Task PostgreSqlFailure_RollsBackEveryRequiredStateAndClearsTracking(string operation, bool failBed, string sqlState, int expected)
    {
        var s = await Seed(admitA: operation != "admit");
        var target = operation == "admit" ? s.Destination : s.SourceA;
        var name = "fail_inpatient_" + Guid.NewGuid().ToString("N");
        var table = failBed ? "beds" : "admissions";
        var action = operation == "admit" && !failBed ? "INSERT" : "UPDATE";
        var column = failBed || operation != "admit" ? "Id" : "BedId";
        var id = failBed ? target.Id.Value : operation == "admit" ? s.Destination.Id.Value : s.AdmissionA!.Id.Value;
        await using var connection = new NpgsqlConnection(database.ConnectionString); await connection.OpenAsync();
        await using var trigger = new NpgsqlCommand($"""
            CREATE FUNCTION inpatient.{name}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
              IF NEW."{column}" = '{id}'::uuid THEN RAISE EXCEPTION 'isolated inpatient write failure' USING ERRCODE = '{sqlState}'; END IF;
              RETURN NEW;
            END; $body$;
            CREATE TRIGGER {name} BEFORE {action} ON inpatient.{table} FOR EACH ROW EXECUTE FUNCTION inpatient.{name}();
            """, connection);
        await trigger.ExecuteNonQueryAsync();
        try
        {
            await using var context = database.Context(); var service = new InpatientTransitionService(context);
            async Task Invoke() {
                if (operation == "admit") await service.AdmitAsync(s.A.Id.Value, s.Destination.Id.Value, "Test Doctor", "Test");
                else if (operation == "transfer") await service.TransferAsync(s.AdmissionA!.Id.Value, s.Destination.Id.Value, "Test");
                else await service.DischargeAsync(s.AdmissionA!.Id.Value, "Test discharge");
            }
            if (expected == 409) await Assert.ThrowsAsync<InpatientConflictException>(Invoke);
            else await Assert.ThrowsAsync<InpatientStorageException>(Invoke);
            Assert.Empty(context.ChangeTracker.Entries()); await context.SaveChangesAsync();
            await AssertOriginal(s);
            await using var host = await Host.Create(database.ConnectionString);
            using var response = operation == "admit" ? await host.Admit(s.A.Id.Value, s.Destination.Id.Value)
                : operation == "transfer" ? await host.Transfer(s.AdmissionA!.Id.Value, s.Destination.Id.Value) : await host.Discharge(s.AdmissionA!.Id.Value);
            Assert.Equal(expected, (int)response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("isolated inpatient write failure", body); Assert.DoesNotContain(name, body);
            await AssertOriginal(s);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP TRIGGER {name} ON inpatient.{table}; DROP FUNCTION inpatient.{name}();", connection);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private async Task AssertOriginal(Setup s)
    {
        await AssertConsistency(s); await using var fresh = database.Context();
        Assert.Equal(BedStatus.Available, (await fresh.Beds.SingleAsync(b => b.Id == s.Destination.Id)).Status);
        if (s.AdmissionA is null) Assert.False(await fresh.Admissions.AnyAsync(a => a.PatientId == s.A.Id));
        else
        {
            var a = await fresh.Admissions.SingleAsync(a => a.Id == s.AdmissionA.Id);
            Assert.Equal(s.SourceA.Id, a.BedId); Assert.False(a.IsDischarged); Assert.Null(a.DischargeDateUtc);
            Assert.Null(a.DischargeSummary); Assert.Equal("Test", a.ChiefComplaint); Assert.Equal(0m, a.TotalRoomCharge);
        }
    }

    [Theory]
    [InlineData("admit")]
    [InlineData("transfer")]
    [InlineData("discharge")]
    public async Task CancellationAfterBothWrites_BeforeCommit_RollsBackTransition(string operation)
    {
        var s = await Seed(admitA: operation != "admit"); await using var context = database.Context(new CancelAfterSave());
        var service = new InpatientTransitionService(context);
        async Task Invoke() {
            if (operation == "admit") await service.AdmitAsync(s.A.Id.Value, s.Destination.Id.Value, "Test Doctor", "Test");
            else if (operation == "transfer") await service.TransferAsync(s.AdmissionA!.Id.Value, s.Destination.Id.Value, "Test");
            else await service.DischargeAsync(s.AdmissionA!.Id.Value, "Test discharge");
        }
        await Assert.ThrowsAsync<OperationCanceledException>(Invoke); Assert.Empty(context.ChangeTracker.Entries()); await AssertOriginal(s);
    }

    [Fact]
    public async Task StaleTrackedAdmission_UsesAuthoritativeCurrentBed()
    {
        var s = await Seed(admitA: true); await using var stale = database.Context();
        await stale.Admissions.Include(a => a.Bed).SingleAsync(a => a.Id == s.AdmissionA!.Id);
        await using (var other = database.Context())
            await new InpatientTransitionService(other).TransferAsync(s.AdmissionA!.Id.Value, s.Destination.Id.Value, "Test");
        await new InpatientTransitionService(stale).DischargeAsync(s.AdmissionA!.Id.Value, "Test discharge");
        await AssertConsistency(s); await using var fresh = database.Context();
        Assert.True((await fresh.Admissions.SingleAsync(a => a.Id == s.AdmissionA!.Id)).IsDischarged);
        Assert.Equal(BedStatus.Available, (await fresh.Beds.SingleAsync(b => b.Id == s.Destination.Id)).Status);
    }

    private async Task<TriageRecord> SeedTriage(PatientId patientId)
    {
        var triage = TriageRecord.Create(patientId, "Test", "120/80", 70, 98, 37, 16, TriageSeverity.Level5_NonUrgent, "Test Nurse");
        await using var context = database.Context(); context.TriageRecords.Add(triage); await context.SaveChangesAsync(); return triage;
    }

    [Fact]
    public async Task EmergencyAdmission_UpdatesTriageAndBedAndRejectsResolvedRepeat()
    {
        var s = await Seed(); var triage = await SeedTriage(s.A.Id);
        await using var host = await Host.Create(database.ConnectionString);
        using var first = await host.Emergency(triage.Id.Value, s.Destination.Id.Value); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var second = await host.Emergency(triage.Id.Value, s.SourceB.Id.Value); Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await AssertConsistency(s); await using var fresh = database.Context();
        var saved = await fresh.TriageRecords.SingleAsync(t => t.Id == triage.Id);
        Assert.Equal(TriageStatus.TransferredToInpatient, saved.Status); Assert.Equal(s.Destination.Id, saved.TransferredBedId);
        Assert.NotNull(saved.DispositionDateUtc);
    }

    [Fact]
    public async Task EmergencyTriageWriteFailure_RollsBackAdmissionBedAndDisposition()
    {
        var s = await Seed(); var triage = await SeedTriage(s.A.Id);
        var name = "fail_er_admission_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(database.ConnectionString); await connection.OpenAsync();
        await using var trigger = new NpgsqlCommand($"""
            CREATE FUNCTION triage.{name}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
              IF NEW."Id" = '{triage.Id.Value}'::uuid THEN RAISE EXCEPTION 'isolated emergency admission failure'; END IF;
              RETURN NEW;
            END; $body$;
            CREATE TRIGGER {name} BEFORE UPDATE ON triage.triage_records FOR EACH ROW EXECUTE FUNCTION triage.{name}();
            """, connection);
        await trigger.ExecuteNonQueryAsync();
        try
        {
            await using var context = database.Context();
            await Assert.ThrowsAsync<InpatientStorageException>(() => new InpatientTransitionService(context)
                .AdmitFromEmergencyAsync(triage.Id.Value, s.Destination.Id.Value, "Test Doctor", "Test"));
            Assert.Empty(context.ChangeTracker.Entries()); await context.SaveChangesAsync(); await AssertOriginal(s);
            await using var fresh = database.Context(); var saved = await fresh.TriageRecords.SingleAsync(t => t.Id == triage.Id);
            Assert.Equal(TriageStatus.Triaged, saved.Status); Assert.Null(saved.TransferredBedId); Assert.Null(saved.DispositionDateUtc);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP TRIGGER {name} ON triage.triage_records; DROP FUNCTION triage.{name}();", connection);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Theory]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.Nurse, true)]
    [InlineData(Roles.Receptionist, true)]
    [InlineData(Roles.Doctor, false)]
    [InlineData(Roles.Patient, false)]
    [InlineData(null, false)]
    public async Task AdmitAuthorization_IsUnchanged(string? role, bool allowed)
    {
        var s = await Seed(); await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Admit(s.A.Id.Value, s.Destination.Id.Value, role);
        Assert.Equal(allowed ? 201 : role is null ? 401 : 403, (int)response.StatusCode); await AssertConsistency(s);
    }

    [Theory]
    [InlineData("transfer", Roles.Nurse, 200)]
    [InlineData("transfer", Roles.Doctor, 403)]
    [InlineData("transfer", Roles.Patient, 403)]
    [InlineData("transfer", null, 401)]
    [InlineData("discharge", Roles.Doctor, 200)]
    [InlineData("discharge", Roles.Receptionist, 200)]
    [InlineData("discharge", Roles.Patient, 403)]
    [InlineData("discharge", null, 401)]
    public async Task TransferAndDischarge_KeepTheirDifferentExistingPolicies(string operation, string? role, int status)
    {
        var s = await Seed(admitA: true); await using var host = await Host.Create(database.ConnectionString);
        using var response = operation == "transfer" ? await host.Transfer(s.AdmissionA!.Id.Value, s.Destination.Id.Value, role)
            : await host.Discharge(s.AdmissionA!.Id.Value, role);
        Assert.Equal(status, (int)response.StatusCode); await AssertConsistency(s);
        if (status != 200) await AssertOriginal(s);
    }

    [Theory]
    [InlineData(Roles.Admin, 201)]
    [InlineData(Roles.Nurse, 403)]
    [InlineData(Roles.Patient, 403)]
    public async Task BedCreation_RemainsAdminOnly(string role, int status)
    {
        await using var host = await Host.Create(database.ConnectionString);
        using var response = await host.Send("/api/v1/inpatient/beds", new { wardName = "Auth-" + Guid.NewGuid().ToString("N"),
            roomNumber = "1", bedNumber = "1", bedType = "General", dailyRate = 100 }, role);
        Assert.Equal(status, (int)response.StatusCode);
    }

    [Fact]
    public void CurrentEfModel_RequiresNoMigration()
    {
        using var context = database.Context(); Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task BedGrid_UsesOneSnapshot_WhenAdmissionCommitsDuringItsRead()
    {
        var s = await Seed();
        var duringRead = new AdmitDuringGridRead(async () => {
            await using var writer = database.Context();
            await new InpatientTransitionService(writer).AdmitAsync(s.A.Id.Value, s.Destination.Id.Value, "Test Doctor", "Test");
        });
        await using var reader = database.Context(duringRead);
        var snapshot = await new GetBedGridQueryHandler(reader).Handle(new GetBedGridQuery(s.Destination.WardName), CancellationToken.None);
        Assert.True(duringRead.Executed);
        var before = Assert.Single(snapshot); Assert.Equal("Available", before.Status); Assert.Null(before.CurrentAdmissionId);
        await using var fresh = database.Context();
        var after = Assert.Single(await new GetBedGridQueryHandler(fresh).Handle(new GetBedGridQuery(s.Destination.WardName), CancellationToken.None));
        Assert.Equal("Occupied", after.Status); Assert.NotNull(after.CurrentAdmissionId); Assert.Equal("Test A", after.OccupiedByPatientName);
        await AssertConsistency(s);
    }

    private sealed class AdmitDuringGridRead(Func<Task> writer) : DbCommandInterceptor
    {
        public bool Executed { get; private set; }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Executed && command.CommandText.Contains("inpatient", StringComparison.Ordinal)
                && command.CommandText.Contains("beds", StringComparison.Ordinal))
            {
                Executed = true; await writer();
            }
            return result;
        }
    }

    private sealed class TwoSaveBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.SetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken); return result;
        }
    }

    private sealed class CancelAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => throw new OperationCanceledException("Test cancellation before commit.");
    }

    private sealed class Host(WebApplication app, HttpClient client, JwtSettings settings) : IAsyncDisposable
    {
        public static async Task<Host> Create(string connection, params IInterceptor[] interceptors)
        {
            var settings = new JwtSettings { Issuer = "InpatientTests", Audience = "InpatientTests",
                Secret = "isolated-inpatient-tests-signing-key-at-least-32-characters" };
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddApplication();
            builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connection).AddInterceptors(interceptors));
            builder.Services.AddScoped<IApplicationDbContext>(p => p.GetRequiredService<ApplicationDbContext>());
            builder.Services.AddScoped<IInpatientTransitionService, InpatientTransitionService>();
            var sessions = new Mock<IJwtSessionValidator>();
            sessions.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, string? version, CancellationToken _) => JwtSessionVersion.Matches(version, "inpatient-test-stamp"));
            builder.Services.AddSingleton(sessions.Object); builder.Services.AddAccountJwtAuthentication(settings);
            builder.Services.AddAuthorization(options => {
                options.AddPolicy(Policies.CanManageAdmissions, p => p.RequireRole(Roles.Admin, Roles.Nurse, Roles.Receptionist));
                options.AddPolicy(Policies.CanEditPatients, p => p.RequireRole(Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist));
                options.AddPolicy(Policies.CanViewPatients, p => p.RequireRole(Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist));
                options.AddPolicy(Policies.CanManageStaff, p => p.RequireRole(Roles.Admin));
            });
            var app = builder.Build(); app.UseMiddleware<GlobalExceptionHandlingMiddleware>(); app.UseAuthentication(); app.UseAuthorization();
            app.MapInpatientEndpoints(); app.MapEmergencyEndpoints(); await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new HttpClient { BaseAddress = new Uri(address) }, settings);
        }

        public Task<HttpResponseMessage> Admit(Guid patient, Guid bed, string? role = Roles.Admin) => Send("/api/v1/inpatient/admissions",
            new { patientId = patient, bedId = bed, admittingDoctorName = "Test Doctor", chiefComplaint = "Test" }, role);
        public Task<HttpResponseMessage> Transfer(Guid admission, Guid bed, string? role = Roles.Admin) => Send($"/api/v1/inpatient/admissions/{admission}/transfer",
            new { newBedId = bed, transferReason = "Test" }, role);
        public Task<HttpResponseMessage> Discharge(Guid admission, string? role = Roles.Admin) => Send($"/api/v1/inpatient/admissions/{admission}/discharge",
            new { dischargeSummary = "Test discharge" }, role);
        public Task<HttpResponseMessage> Emergency(Guid triage, Guid bed) => Send($"/api/v1/emergency/triage/{triage}/transfer-inpatient",
            new { bedId = bed, admittingDoctorName = "Test Doctor", admissionNotes = "Test" });
        public async Task<HttpResponseMessage> Send(string route, object payload, string? role = Roles.Admin)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(payload) };
            if (role is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtTokenGenerator(Options.Create(settings))
                .GenerateToken(Guid.NewGuid().ToString(), "test@example.invalid", "Test", "User", [role], "inpatient-test-stamp"));
            return await client.SendAsync(request);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
