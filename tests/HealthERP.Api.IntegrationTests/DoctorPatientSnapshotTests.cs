using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HealthERP.Api.Authentication;
using HealthERP.Api.Endpoints;
using HealthERP.Api.Middleware;
using HealthERP.Application;
using HealthERP.Application.Common.Configurations;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Common.Security;
using HealthERP.Application.DoctorWorkspace.Models;
using HealthERP.Application.DoctorWorkspace.Queries;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Laboratory;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Pharmacy;
using HealthERP.Domain.StaffMembers;
using HealthERP.Infrastructure.Identity;
using HealthERP.Infrastructure.Persistence;
using HealthERP.Infrastructure.Services;
using HealthERP.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Staff = HealthERP.Domain.StaffMembers.StaffMembers;

namespace HealthERP.Api.IntegrationTests;

[Collection("Pharmacy PostgreSQL")]
public partial class DoctorPatientSnapshotTests(PharmacyPostgresFixture database)
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private const string UntrustedAuthor = "Untrusted Browser Doctor";

    private async Task<(ApplicationUser User, Staff Staff)> Doctor(string[]? roles = null, string state = "valid")
    {
        await using var db = database.Context(); var id = Guid.NewGuid(); var email = id.ToString("N") + "@example.invalid";
        var user = new ApplicationUser { Id = id, UserName = email, NormalizedUserName = email.ToUpperInvariant(),
            Email = email, NormalizedEmail = email.ToUpperInvariant(), FirstName = "Identity", LastName = "Account",
            IsActive = state != "inactive-account", SecurityStamp = Guid.NewGuid().ToString("N"), CreatedAtUtc = DateTime.UtcNow };
        if (state == "locked-account") { user.LockoutEnabled = true; user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1); }
        db.Users.Add(user);
        foreach (var name in roles ?? [Roles.Doctor])
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == name);
            if (role is null) { role = new ApplicationRole(name) { Id = Guid.NewGuid(), NormalizedName = name.ToUpperInvariant() }; db.Roles.Add(role); }
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = id, RoleId = role.Id });
        }
        var staff = Staff.Create("Verified", "Doctor", email, "", "General", "", "Outpatient"); staff.CreatedAtUtc = DateTime.UtcNow;
        if (state != "unlinked") staff.LinkUserId(user.Id);
        staff.AssignStaffKind(state == "unspecified" ? StaffKind.Unspecified : StaffKind.Doctor);
        if (state == "inactive-staff") staff.Deactivate();
        db.Staff.Add(staff); if (state == "deleted-staff") db.Entry(staff).Property(s => s.IsDeleted).CurrentValue = true;
        await db.SaveChangesAsync(); return (user, staff);
    }
    private async Task<Patient> Patient(bool deleted = false)
    {
        await using var db = database.Context();
        var p = HealthERP.Domain.Patients.Patient.Register("SN-" + Guid.NewGuid().ToString("N")[..16], "Persisted", "Patient", new DateOnly(1987, 4, 3),
            "Female", "private-contact@example.invalid", "private-phone");
        p.CreatedAtUtc = DateTime.UtcNow; db.Patients.Add(p);
        if (deleted) db.Entry(p).Property(x => x.IsDeleted).CurrentValue = true;
        await db.SaveChangesAsync(); return p;
    }
    private async Task<Appointment> Relationship(Staff staff, Patient patient, AppointmentStatus status = AppointmentStatus.Scheduled,
        bool deleted = false, int days = 0)
    {
        await using var db = database.Context();
        var a = Appointment.Schedule(patient.Id, staff.Id, Now.AddDays(days), Now.AddDays(days).AddMinutes(30), "Actual consultation");
        a.CreatedAtUtc = DateTime.UtcNow;
        switch (status) { case AppointmentStatus.Confirmed: a.Confirm(); break; case AppointmentStatus.Completed: a.Complete(); break;
            case AppointmentStatus.Cancelled: a.Cancel(); break; case AppointmentStatus.NoShow: a.MarkNoShow(); break; }
        db.Appointments.Add(a); if (deleted) db.Entry(a).Property(x => x.IsDeleted).CurrentValue = true;
        await db.SaveChangesAsync(); return a;
    }
    private async Task<ClinicalNote> Note(Patient patient, DateTime date, string text = "Actual note", string? bp = null,
        int? pulse = null, decimal? temperature = null, decimal? weight = null)
    {
        await using var db = database.Context();
        var n = ClinicalNote.Create(patient.Id, UntrustedAuthor, "Persisted complaint", "Persisted diagnosis", text, bp, pulse, temperature, weight);
        n.CreatedAtUtc = DateTime.UtcNow; db.ClinicalNotes.Add(n); db.Entry(n).Property(x => x.NoteDateUtc).CurrentValue = date;
        await db.SaveChangesAsync(); return n;
    }
    private async Task<Prescription> Prescription(Patient patient, DateTime date, PrescriptionStatus status = PrescriptionStatus.Pending, string medication = "Persisted medication")
    {
        await using var db = database.Context();
        var p = HealthERP.Domain.Pharmacy.Prescription.Create(patient.Id, null, UntrustedAuthor, medication, "5 mg", "Twice daily", 10, 2, "Persisted instructions");
        if (status == PrescriptionStatus.Dispensed) p.Dispense(); else if (status == PrescriptionStatus.Cancelled) p.Cancel();
        p.CreatedAtUtc = DateTime.UtcNow; db.Prescriptions.Add(p); db.Entry(p).Property(x => x.PrescribedDateUtc).CurrentValue = date;
        await db.SaveChangesAsync(); return p;
    }
    private async Task<LabOrder> Lab(Patient patient, DateTime date, LabOrderStatus status = LabOrderStatus.Ordered, string name = "Persisted test")
    {
        await using var db = database.Context(); var l = LabOrder.Create(patient.Id, null, UntrustedAuthor, name, "TEST", "Urgent", "4-8", "unit");
        if (status == LabOrderStatus.Completed) l.RecordResult("9.4", "Persisted remarks", UntrustedAuthor, true);
        else if (status == LabOrderStatus.Cancelled) l.Cancel(); else if (status == LabOrderStatus.InProgress) l.MarkInProgress(UntrustedAuthor);
        l.CreatedAtUtc = DateTime.UtcNow; db.LabOrders.Add(l); db.Entry(l).Property(x => x.OrderedDateUtc).CurrentValue = date;
        await db.SaveChangesAsync(); return l;
    }
    private async Task<Admission> Admission(Patient patient, DateTime date, bool discharged = false, bool inconsistentDate = false)
    {
        await using var db = database.Context(); var bed = Bed.Create("Snapshot tests", Guid.NewGuid().ToString("N"), "1", "General", 123m);
        bed.CreatedAtUtc = DateTime.UtcNow; db.Beds.Add(bed);
        var a = HealthERP.Domain.Inpatient.Admission.Create(patient.Id, bed.Id, UntrustedAuthor, "Persisted admission complaint", 123m);
        if (discharged) a.Discharge("Private discharge text"); a.CreatedAtUtc = DateTime.UtcNow; db.Admissions.Add(a);
        db.Entry(a).Property(x => x.AdmissionDateUtc).CurrentValue = date;
        if (inconsistentDate) db.Entry(a).Property(x => x.DischargeDateUtc).CurrentValue = Now;
        await db.SaveChangesAsync(); return a;
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled, -365)] [InlineData(AppointmentStatus.Scheduled, 0)] [InlineData(AppointmentStatus.Scheduled, 365)]
    [InlineData(AppointmentStatus.Confirmed, -365)] [InlineData(AppointmentStatus.Confirmed, 0)] [InlineData(AppointmentStatus.Confirmed, 365)]
    [InlineData(AppointmentStatus.Completed, -365)] [InlineData(AppointmentStatus.Completed, 0)] [InlineData(AppointmentStatus.Completed, 365)]
    public async Task AnyDateAllowedStatusGrantsV1Access(AppointmentStatus status, int days)
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient, status, days: days);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value);
        Assert.Equal(200, (int)response.StatusCode); Assert.Equal(patient.Id.Value, (await Read(response)).Patient.PatientId);
    }
    [Theory] [InlineData(AppointmentStatus.Cancelled)] [InlineData(AppointmentStatus.NoShow)]
    public async Task TerminalNonCareStatusDoesNotGrantAccess(AppointmentStatus status)
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient, status);
        await Note(patient, Now); await using var api = await Api.Create(database.ConnectionString);
        using var response = await api.Get(doctor.User, patient.Id.Value); Assert.Equal(404, (int)response.StatusCode); AssertNoClinicalRead(api);
    }
    [Fact]
    public async Task SoftDeletedAppointmentDoesNotGrantAccess()
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient, deleted: true);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value);
        Assert.Equal(404, (int)response.StatusCode); AssertNoClinicalRead(api);
    }
    [Fact]
    public async Task DoctorsAreIndependentAndCallerCannotOverrideOwnership()
    {
        var a = await Doctor(); var b = await Doctor(); var x = await Patient(); var y = await Patient();
        await Relationship(a.Staff, x); await Relationship(b.Staff, y);
        await using var api = await Api.Create(database.ConnectionString);
        using var ax = await api.Get(a.User, x.Id.Value); Assert.Equal(x.Id.Value, (await Read(ax)).Patient.PatientId);
        using var ay = await api.Get(a.User, y.Id.Value, $"?staffId={b.Staff.Id.Value}&doctorId={b.Staff.Id.Value}&userId={b.User.Id}");
        Assert.Equal(404, (int)ay.StatusCode);
        using var by = await api.Get(b.User, y.Id.Value); Assert.Equal(y.Id.Value, (await Read(by)).Patient.PatientId);
        using var bx = await api.Get(b.User, x.Id.Value); Assert.Equal(404, (int)bx.StatusCode);
        Assert.Equal(new[] { "PatientId" }, typeof(GetDoctorPatientSnapshotQuery).GetProperties().Select(p => p.Name));
    }
    [Fact]
    public async Task NoRelationshipAndNonexistentPatientHaveSameSafeProblemDetails()
    {
        var doctor = await Doctor(); var patient = await Patient(); await Note(patient, Now);
        await using var api = await Api.Create(database.ConnectionString);
        using var existing = await api.Get(doctor.User, patient.Id.Value); using var absent = await api.Get(doctor.User, Guid.NewGuid());
        Assert.Equal(404, (int)existing.StatusCode); Assert.Equal(existing.StatusCode, absent.StatusCode);
        using var a = JsonDocument.Parse(await existing.Content.ReadAsStringAsync()); using var b = JsonDocument.Parse(await absent.Content.ReadAsStringAsync());
        foreach (var field in new[] { "title", "status", "detail" }) Assert.Equal(a.RootElement.GetProperty(field).ToString(), b.RootElement.GetProperty(field).ToString());
        Assert.Equal("Patient snapshot was not found.", a.RootElement.GetProperty("detail").GetString()); AssertNoClinicalRead(api);
    }
    [Fact]
    public async Task LegacyAuthorTextDoesNotGrantAccess()
    {
        var doctor = await Doctor(); var patient = await Patient();
        await Note(patient, Now); await Prescription(patient, Now); await Lab(patient, Now); await Admission(patient, Now.AddDays(-1));
        await using (var db = database.Context())
        {
            foreach (var n in await db.ClinicalNotes.Where(n => n.PatientId == patient.Id).ToListAsync()) db.Entry(n).Property(x => x.DoctorName).CurrentValue = "Verified Doctor";
            foreach (var p in await db.Prescriptions.Where(n => n.PatientId == patient.Id).ToListAsync()) db.Entry(p).Property(x => x.DoctorName).CurrentValue = "Verified Doctor";
            foreach (var l in await db.LabOrders.Where(n => n.PatientId == patient.Id).ToListAsync()) db.Entry(l).Property(x => x.OrderingDoctorName).CurrentValue = "Verified Doctor";
            await db.SaveChangesAsync();
        }
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value);
        Assert.Equal(404, (int)response.StatusCode); AssertNoClinicalRead(api);
    }
    [Theory] [InlineData("unlinked", 403)] [InlineData("unspecified", 403)] [InlineData("inactive-staff", 403)]
    [InlineData("deleted-staff", 403)] [InlineData("inactive-account", 401)] [InlineData("locked-account", 401)]
    public async Task IneligibleDoctorIsDeniedBeforeRelationshipRead(string state, int expected)
    {
        var doctor = await Doctor(state: state); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value);
        Assert.Equal(expected, (int)response.StatusCode); Assert.DoesNotContain(api.Sql.Commands, s => s.Text.Contains("scheduling.appointments")); AssertNoClinicalRead(api);
    }
    [Theory] [InlineData(null, 401)] [InlineData(Roles.Admin, 403)] [InlineData(Roles.Nurse, 403)] [InlineData(Roles.Patient, 403)]
    public async Task RouteRequiresAuthenticatedDoctor(string? role, int expected)
    {
        var doctor = await Doctor(role is null ? [] : [role]); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        await using var api = await Api.Create(database.ConnectionString);
        using var response = await api.Get(doctor.User, patient.Id.Value, roles: role is null ? null : [role], anonymous: role is null);
        Assert.Equal(expected, (int)response.StatusCode); AssertNoClinicalRead(api);
    }
    [Fact]
    public async Task StaleDoctorClaimDoesNotReplaceLiveRole()
    {
        var doctor = await Doctor([Roles.Nurse]); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value, roles: [Roles.Doctor]);
        Assert.Equal(403, (int)response.StatusCode); AssertNoClinicalRead(api);
    }
    [Fact]
    public async Task RevokedSessionIsDeniedBeforeSnapshotRead()
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        await using (var db = database.Context()) { (await db.Users.SingleAsync(u => u.Id == doctor.User.Id)).SecurityStamp = Guid.NewGuid().ToString("N"); await db.SaveChangesAsync(); }
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value);
        Assert.Equal(401, (int)response.StatusCode); AssertNoClinicalRead(api);
    }
    [Fact]
    public async Task AdminDoctorStillRequiresOwnRelationship()
    {
        var own = await Doctor([Roles.Admin, Roles.Doctor]); var other = await Doctor(); var x = await Patient(); var y = await Patient();
        await Relationship(own.Staff, x); await Relationship(other.Staff, y); await using var api = await Api.Create(database.ConnectionString);
        using var yes = await api.Get(own.User, x.Id.Value, roles: [Roles.Admin, Roles.Doctor]); Assert.Equal(x.Id.Value, (await Read(yes)).Patient.PatientId);
        using var no = await api.Get(own.User, y.Id.Value, roles: [Roles.Admin, Roles.Doctor]); Assert.Equal(404, (int)no.StatusCode);
    }
    [Fact]
    public async Task DeletedPatientIsUnavailableDespiteRelationship()
    {
        var doctor = await Doctor(); var patient = await Patient(deleted: true); await Relationship(doctor.Staff, patient); await Note(patient, Now);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value);
        Assert.Equal(404, (int)response.StatusCode); Assert.DoesNotContain(api.Sql.Commands, c => c.Text.Contains("clinical_notes"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Assert.Equal("Patient snapshot was not found.", json.RootElement.GetProperty("detail").GetString());
    }
    [Fact]
    public async Task IdentityIsPersistedAndMissingClinicalDataStaysMissing()
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value); var dto = await Read(response);
        Assert.Equal(new SnapshotPatientDto(patient.Id.Value, patient.MedicalRecordNumber, "Persisted Patient", patient.DateOfBirth, "Female"), dto.Patient);
        Assert.Null(dto.LatestVitals); Assert.Empty(dto.RecentNotes); Assert.Empty(dto.RecentPrescriptions); Assert.Empty(dto.RecentLabOrders); Assert.Null(dto.CurrentAdmission);
    }
    [Fact]
    public async Task LatestVitalsAreOneRealPartialObservationEvenOutsideRecentFiveNotes()
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        await Note(patient, Now.AddDays(-12), bp: "131/89", pulse: 83, temperature: 38.2m, weight: 61.4m);
        await Note(patient, Now.AddDays(-10), pulse: 91);
        for (var i = 0; i < 7; i++) await Note(patient, Now.AddDays(-i), bp: "   ");
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value); var dto = await Read(response);
        Assert.Equal(new SnapshotVitalsDto(null, 91, null, null, Now.AddDays(-10)), dto.LatestVitals); Assert.Equal(5, dto.RecentNotes.Count);
    }
    [Theory] [InlineData("bp")] [InlineData("pulse")] [InlineData("temperature")] [InlineData("weight")]
    public async Task EachPersistedVitalCanEstablishLatestObservation(string field)
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        await Note(patient, Now, bp: field == "bp" ? "139/91" : null, pulse: field == "pulse" ? 0 : null,
            temperature: field == "temperature" ? 0m : null, weight: field == "weight" ? 0m : null);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value); var vitals = (await Read(response)).LatestVitals;
        Assert.NotNull(vitals); Assert.Equal(Now, vitals.RecordedAtUtc);
        Assert.Equal(field == "bp" ? "139/91" : null, vitals.BloodPressure); Assert.Equal(field == "pulse" ? 0 : (int?)null, vitals.HeartRateBpm);
        Assert.Equal(field == "temperature" ? 0m : (decimal?)null, vitals.TemperatureCelsius); Assert.Equal(field == "weight" ? 0m : (decimal?)null, vitals.WeightKg);
    }
    [Fact]
    public async Task AllHistoriesAreBoundedNewestFirstAndDeterministicWithTies()
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        var notes = new List<ClinicalNote>(); var rx = new List<Prescription>(); var labs = new List<LabOrder>();
        for (var i = 0; i < 8; i++)
        {
            var date = i < 2 ? Now : Now.AddHours(-i);
            notes.Add(await Note(patient, date, "Persisted note " + i)); rx.Add(await Prescription(patient, date)); labs.Add(await Lab(patient, date));
        }
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value); var dto = await Read(response);
        Assert.Equal(notes.OrderByDescending(n => n.NoteDateUtc).ThenByDescending(n => n.Id.Value.ToString(), StringComparer.Ordinal).Take(5).Select(n => n.Id.Value), dto.RecentNotes.Select(n => n.NoteId));
        Assert.Equal(rx.OrderByDescending(p => p.PrescribedDateUtc).ThenByDescending(p => p.Id.Value.ToString(), StringComparer.Ordinal).Take(5).Select(p => p.Id.Value), dto.RecentPrescriptions.Select(p => p.PrescriptionId));
        Assert.Equal(labs.OrderByDescending(l => l.OrderedDateUtc).ThenByDescending(l => l.Id.Value.ToString(), StringComparer.Ordinal).Take(5).Select(l => l.Id.Value), dto.RecentLabOrders.Select(l => l.LabOrderId));
    }
    [Theory] [InlineData(PrescriptionStatus.Pending)] [InlineData(PrescriptionStatus.Dispensed)] [InlineData(PrescriptionStatus.Cancelled)]
    public async Task RecentPrescriptionsReportActualStatusWithoutActiveMedicationClaims(PrescriptionStatus status)
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient); var p = await Prescription(patient, Now, status);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value); var item = Assert.Single((await Read(response)).RecentPrescriptions);
        Assert.Equal(p.Status.ToString(), item.Status); Assert.Equal(p.MedicationName, item.MedicationName); Assert.Equal(p.Dosage, item.Dosage);
        Assert.Equal(p.Frequency, item.Frequency); Assert.Equal(p.DurationDays, item.DurationDays); Assert.Equal(p.Refills, item.Refills); Assert.Equal(p.Instructions, item.Instructions);
        Assert.Equal(p.DispensedDateUtc?.Ticks / 10, item.DispensedDateUtc?.Ticks / 10);
    }
    [Theory] [InlineData(LabOrderStatus.Ordered)] [InlineData(LabOrderStatus.InProgress)] [InlineData(LabOrderStatus.Completed)] [InlineData(LabOrderStatus.Cancelled)]
    public async Task RecentLabActivityUsesRealStatusesAndOnlyRecordedCompletedResults(LabOrderStatus status)
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient); var lab = await Lab(patient, Now, status);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value); var item = Assert.Single((await Read(response)).RecentLabOrders);
        Assert.Equal(lab.Status.ToString(), item.Status); Assert.Equal(lab.TestName, item.TestName); Assert.Equal(lab.Priority, item.Priority);
        if (status != LabOrderStatus.Completed) Assert.Null(item.Result);
        else { Assert.NotNull(item.Result); Assert.Equal("9.4", item.Result.Value); Assert.Equal("4-8", item.Result.ReferenceRange);
            Assert.Equal("unit", item.Result.Unit); Assert.True(item.Result.IsAbnormal); Assert.Equal("Persisted remarks", item.Result.Remarks); Assert.NotNull(item.Result.CompletedDateUtc); }
    }
    [Theory] [InlineData("none")] [InlineData("discharged")] [InlineData("future")] [InlineData("inconsistent")]
    public async Task HistoricalOrInvalidAdmissionIsNeverPresentedAsCurrent(string state)
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        if (state != "none") await Admission(patient, state == "future" ? Now.AddDays(1) : Now.AddDays(-2), state == "discharged", state == "inconsistent");
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value); Assert.Null((await Read(response)).CurrentAdmission);
    }
    [Fact]
    public async Task CurrentAdmissionReflectsPersistedOpenStateWithoutFinancialFields()
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        await Admission(patient, Now.AddDays(-5), discharged: true); var current = await Admission(patient, Now.AddDays(-1));
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value); var item = (await Read(response)).CurrentAdmission;
        Assert.Equal(new SnapshotAdmissionDto(current.Id.Value, Now.AddDays(-1), current.ChiefComplaint), item);
    }
    [Fact]
    public async Task HistoriesFromAnotherPatientNeverLeak()
    {
        var doctor = await Doctor(); var own = await Patient(); var unrelated = await Patient(); await Relationship(doctor.Staff, own);
        await Note(unrelated, Now, "UNRELATED CLINICAL NOTE", pulse: 88); await Prescription(unrelated, Now, medication: "UNRELATED MEDICATION");
        await Lab(unrelated, Now, name: "UNRELATED LAB"); await Admission(unrelated, Now.AddDays(-1));
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, own.Id.Value); var dto = await Read(response);
        Assert.Empty(dto.RecentNotes); Assert.Empty(dto.RecentPrescriptions); Assert.Empty(dto.RecentLabOrders); Assert.Null(dto.CurrentAdmission); Assert.Null(dto.LatestVitals);
    }
    [Fact]
    public async Task ExactResponsePropertySetsExcludeAuthorIdentityContactBillingAndPortalData()
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        // A real linked Patient plus real billing rows must still not leak linkage or financial information.
        await using (var db = database.Context())
        {
            var p = await db.Patients.SingleAsync(p => p.Id == patient.Id); p.LinkUserId(doctor.User.Id);
            var invoice = HealthERP.Domain.Billing.Invoice.Create(patient.Id, "SN-" + Guid.NewGuid().ToString("N"), Now.AddDays(1));
            invoice.AddLineItem("Private billing line", 1, 99m); db.Invoices.Add(invoice); await db.SaveChangesAsync();
        }
        await Note(patient, Now, bp: "138/86", pulse: 85, temperature: 38.1m, weight: 62m); await Prescription(patient, Now);
        await Lab(patient, Now, LabOrderStatus.Completed); await Admission(patient, Now.AddDays(-1));
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value); response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(); using var json = JsonDocument.Parse(body); var root = json.RootElement;
        Properties(root, "patient", "latestVitals", "recentNotes", "recentPrescriptions", "recentLabOrders", "currentAdmission");
        Properties(root.GetProperty("patient"), "patientId", "medicalRecordNumber", "displayName", "dateOfBirth", "gender");
        Properties(root.GetProperty("latestVitals"), "bloodPressure", "heartRateBpm", "temperatureCelsius", "weightKg", "recordedAtUtc");
        Properties(root.GetProperty("recentNotes")[0], "noteId", "recordedAtUtc", "chiefComplaint", "diagnosis", "notes", "recordedByDisplayName");
        Properties(root.GetProperty("recentPrescriptions")[0], "prescriptionId", "prescribedDateUtc", "medicationName", "dosage", "frequency", "durationDays", "refills", "instructions", "status", "dispensedDateUtc");
        var lab = root.GetProperty("recentLabOrders")[0]; Properties(lab, "labOrderId", "orderedDateUtc", "testName", "testCode", "priority", "status", "result");
        Properties(lab.GetProperty("result"), "completedDateUtc", "value", "referenceRange", "unit", "remarks", "isAbnormal");
        Properties(root.GetProperty("currentAdmission"), "admissionId", "admissionDateUtc", "chiefComplaint");
        Assert.DoesNotContain(UntrustedAuthor, body); Assert.DoesNotContain("private-contact", body); Assert.DoesNotContain("Private billing", body);
        Assert.DoesNotContain(api.Sql.Commands, c => c.Text.Contains("billing."));
    }
    [Fact]
    public async Task SqlAuthorizesBeforeLoadingClinicalDataAndLimitsAllHistoryInDatabase()
    {
        var doctor = await Doctor(); var patient = await Patient(); await Relationship(doctor.Staff, patient);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, patient.Id.Value); await Read(response);
        var commands = api.Sql.Commands; var access = Assert.Single(commands, c => c.Text.Contains("scheduling.appointments"));
        Assert.Contains("EXISTS", access.Text); Assert.Contains("\"StaffId\" =", access.Text); Assert.Contains("\"PatientId\" =", access.Text);
        Assert.Contains("NOT", access.Text); Assert.Contains("\"IsDeleted\"", access.Text); Assert.Contains("\"Status\" IN (1, 2, 3)", access.Text);
        Assert.Contains(doctor.Staff.Id.Value, access.Parameters.Values); Assert.Contains(patient.Id.Value, access.Parameters.Values);
        var accessIndex = commands.IndexOf(access); var patientIndex = commands.FindIndex(c => c.Text.Contains("patients.patients")); Assert.True(patientIndex > accessIndex);
        foreach (var table in new[] { "clinical_notes", "pharmacy.prescriptions", "laboratory.lab_orders", "inpatient.admissions" })
        {
            var reads = commands.Where(c => c.Text.Contains(table)).ToArray(); Assert.NotEmpty(reads);
            foreach (var c in reads) { Assert.True(commands.IndexOf(c) > patientIndex); Assert.Contains("\"PatientId\" =", c.Text);
                Assert.Contains(patient.Id.Value, c.Parameters.Values); Assert.Contains("ORDER BY", c.Text); Assert.Contains("LIMIT", c.Text); }
        }
        foreach (var c in commands.Skip(patientIndex)) foreach (var field in new[] { "\"UserId\"", "\"Email\"", "\"PhoneNumber\"", "\"DoctorName\"", "\"OrderingDoctorName\"", "\"AdmittingDoctorName\"", "\"PerformedBy\"", "\"CreatedBy\"" }) Assert.DoesNotContain(field, c.Text);
    }
    [Fact]
    public async Task MalformedPatientIdReturnsBadRequest()
    {
        var doctor = await Doctor(); await using var api = await Api.Create(database.ConnectionString);
        using var response = await api.GetPath(doctor.User, "not-a-guid"); Assert.Equal(400, (int)response.StatusCode); AssertNoClinicalRead(api);
    }
    [Fact] public void ModelHasNoPendingChangesAndClinicalModelsHaveNoSoftDeleteFlag()
    {
        using var db = database.Context(); Assert.False(db.Database.HasPendingModelChanges());
        foreach (var type in new[] { typeof(ClinicalNote), typeof(Prescription), typeof(LabOrder), typeof(Admission) })
            Assert.Null(db.Model.FindEntityType(type)!.FindProperty("IsDeleted"));
    }

    private static void Properties(JsonElement element, params string[] names) => Assert.Equal(names.Order(), element.EnumerateObject().Select(p => p.Name).Order());
    private static void AssertNoClinicalRead(Api api) => Assert.DoesNotContain(api.Sql.Commands,
        c => new[] { "patients.patients", "clinical_notes", "pharmacy.prescriptions", "laboratory.lab_orders", "inpatient.admissions" }.Any(c.Text.Contains));
    private static async Task<DoctorPatientSnapshotDto> Read(HttpResponseMessage response)
    { Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); return (await response.Content.ReadFromJsonAsync<DoctorPatientSnapshotDto>())!; }

    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed record SqlRead(string Text, Dictionary<string, object?> Parameters);
    private sealed class SqlCapture : DbCommandInterceptor
    {
        public List<SqlRead> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(new(command.CommandText, command.Parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName, p => p.Value)));
            return ValueTask.FromResult(result);
        }
    }
    private sealed class Api(WebApplication app, HttpClient client, JwtSettings settings, SqlCapture sql) : IAsyncDisposable
    {
        public SqlCapture Sql => sql;
        public static async Task<Api> Create(string connection)
        {
            var settings = new JwtSettings { Issuer = "DoctorSnapshotTests", Audience = "DoctorSnapshotTests", Secret = "doctor-snapshot-isolated-signing-secret-at-least-32-characters" };
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddApplication(); builder.Services.AddHttpContextAccessor(); var sql = new SqlCapture();
            builder.Services.AddScoped<AuditableEntitySaveChangesInterceptor>();
            builder.Services.AddDbContext<ApplicationDbContext>((sp,o) => o.UseNpgsql(connection).AddInterceptors(sql, sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>()));
            builder.Services.AddScoped<IApplicationDbContext>(p => p.GetRequiredService<ApplicationDbContext>());
            builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>(); builder.Services.AddScoped<ICurrentStaffService, CurrentStaffService>();
            builder.Services.AddScoped<IJwtSessionValidator, JwtSessionValidator>(); builder.Services.AddSingleton<TimeProvider>(new FixedClock());
            builder.Services.AddSingleton(new FacilityTimeZone("Asia/Karachi")); builder.Services.AddAccountJwtAuthentication(settings); builder.Services.AddAuthorization();
            builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
            var app = builder.Build(); app.UseMiddleware<GlobalExceptionHandlingMiddleware>(); app.UseAuthentication(); app.UseAuthorization(); app.MapDoctorEndpoints();
            await app.StartAsync(); var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new HttpClient { BaseAddress = new Uri(address) }, settings, sql);
        }
        public Task<HttpResponseMessage> Get(ApplicationUser user, Guid patientId, string query = "", string[]? roles = null, bool anonymous = false)
            => GetPath(user, patientId.ToString() + "/snapshot" + query, roles, anonymous, hasSuffix: true);
        public async Task<HttpResponseMessage> Post(ApplicationUser user, Guid patientId, object body, string[]? roles = null, bool anonymous = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/doctor/patients/{patientId}/clinical-notes") { Content = JsonContent.Create(body) };
            if (!anonymous) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtTokenGenerator(Options.Create(settings))
                .GenerateToken(user.Id.ToString(), user.Email!, user.FirstName, user.LastName, roles ?? [Roles.Doctor], user.SecurityStamp!));
            return await client.SendAsync(request);
        }
        public async Task<HttpResponseMessage> GetPath(ApplicationUser user, string path, string[]? roles = null, bool anonymous = false, bool hasSuffix = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/doctor/patients/" + path + (hasSuffix ? "" : "/snapshot"));
            if (!anonymous) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtTokenGenerator(Options.Create(settings))
                .GenerateToken(user.Id.ToString(), user.Email!, user.FirstName, user.LastName, roles ?? [Roles.Doctor], user.SecurityStamp!));
            return await client.SendAsync(request);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
