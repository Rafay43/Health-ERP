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
using HealthERP.Domain.Patients;
using HealthERP.Domain.StaffMembers;
using HealthERP.Infrastructure.Identity;
using HealthERP.Infrastructure.Persistence;
using HealthERP.Infrastructure.Services;
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
public class DoctorScheduleTests(PharmacyPostgresFixture database)
{
    private static readonly DateOnly Day = new(2026, 10, 22);
    private static readonly DateTime Now = new(2026, 10, 22, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2026, 10, 21, 19, 0, 0, DateTimeKind.Utc);

    private async Task<(ApplicationUser User, Staff Staff)> Doctor(string[]? roles = null, string state = "valid")
    {
        await using var db = database.Context();
        var id = Guid.NewGuid(); var email = id.ToString("N") + "@example.invalid";
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
        var staff = Staff.Create("Verified", "Doctor", email, "", "Cardiology", "", "Outpatient");
        staff.CreatedAtUtc = DateTime.UtcNow;
        if (state != "unlinked") staff.LinkUserId(user.Id);
        staff.AssignStaffKind(state == "unspecified" ? StaffKind.Unspecified : StaffKind.Doctor);
        if (state == "inactive-staff") staff.Deactivate();
        db.Staff.Add(staff);
        if (state == "deleted-staff") db.Entry(staff).Property(s => s.IsDeleted).CurrentValue = true;
        await db.SaveChangesAsync(); return (user, staff);
    }

    private async Task<Appointment> Appointment(Staff staff, DateTime start, AppointmentStatus status = AppointmentStatus.Scheduled,
        bool deleted = false, bool deletedPatient = false)
    {
        await using var db = database.Context();
        var patient = Patient.Register("DS-" + Guid.NewGuid().ToString("N")[..16], "Scheduled", "Patient", new DateOnly(1990, 1, 1),
            "Female", "private@example.invalid", "private-phone");
        patient.CreatedAtUtc = DateTime.UtcNow; db.Patients.Add(patient);
        if (deletedPatient) db.Entry(patient).Property(p => p.IsDeleted).CurrentValue = true;
        var appointment = HealthERP.Domain.Appointments.Appointment.Schedule(patient.Id, staff.Id, start, start.AddMinutes(30), "Persisted reason", "Private appointment notes");
        appointment.CreatedAtUtc = DateTime.UtcNow;
        switch (status) { case AppointmentStatus.Confirmed: appointment.Confirm(); break;
            case AppointmentStatus.Completed: appointment.Complete(); break;
            case AppointmentStatus.Cancelled: appointment.Cancel(); break;
            case AppointmentStatus.NoShow: appointment.MarkNoShow(); break; }
        db.Appointments.Add(appointment);
        if (deleted) db.Entry(appointment).Property(a => a.IsDeleted).CurrentValue = true;
        await db.SaveChangesAsync(); return appointment;
    }

    [Fact]
    public async Task DoctorsIndependentlyReceiveOnlyOwnAppointmentsAndCannotOverrideOwnership()
    {
        var a = await Doctor(); var b = await Doctor(); var aa = await Appointment(a.Staff, Now.AddHours(1)); var bb = await Appointment(b.Staff, Now.AddHours(1));
        await using var api = await Api.Create(database.ConnectionString);
        using var ar = await api.Get(a.User, $"?date=2026-10-22&staffId={b.Staff.Id.Value}&doctorId={b.Staff.Id.Value}&userId={b.User.Id}");
        var ad = (await ar.Content.ReadFromJsonAsync<DoctorScheduleDto>())!;
        Assert.Equal(200, (int)ar.StatusCode); Assert.Equal(aa.Id.Value, Assert.Single(ad.Appointments).AppointmentId);
        using var br = await api.Get(b.User); Assert.Equal(bb.Id.Value, Assert.Single((await br.Content.ReadFromJsonAsync<DoctorScheduleDto>())!.Appointments).AppointmentId);
        Assert.Equal(new[] { "Date" }, typeof(GetDoctorScheduleQuery).GetProperties().Select(p => p.Name));
        var queries = api.Sql.Commands.Where(s => s.Contains("scheduling.appointments")).ToArray();
        Assert.Equal(2, queries.Length);
        var sql = queries[0];
        Assert.Contains("\"StaffId\" =", sql); Assert.Contains("\"StartDateTimeUtc\" >=", sql); Assert.Contains("\"StartDateTimeUtc\" <", sql);
        Assert.Contains("ORDER BY", sql); Assert.DoesNotContain("\"Email\"", sql); Assert.DoesNotContain("\"Notes\"", sql);
    }

    [Theory]
    [InlineData("unlinked", 403)] [InlineData("unspecified", 403)] [InlineData("inactive-staff", 403)]
    [InlineData("deleted-staff", 403)] [InlineData("inactive-account", 401)] [InlineData("locked-account", 401)]
    public async Task IneligibleDoctorFailsClosed(string state, int expected)
    {
        var doctor = await Doctor(state: state); await using var api = await Api.Create(database.ConnectionString);
        using var response = await api.Get(doctor.User); Assert.Equal(expected, (int)response.StatusCode);
        Assert.DoesNotContain(api.Sql.Commands, s => s.Contains("scheduling.appointments"));
    }

    [Theory] [InlineData(null, 401)] [InlineData(Roles.Admin, 403)] [InlineData(Roles.Nurse, 403)] [InlineData(Roles.Patient, 403)]
    public async Task RouteRequiresAuthenticatedDoctor(string? role, int expected)
    {
        var doctor = await Doctor(role is null ? [] : [role]); await using var api = await Api.Create(database.ConnectionString);
        using var response = await api.Get(doctor.User, roles: role is null ? null : [role], anonymous: role is null);
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Fact]
    public async Task StaleDoctorClaimCannotReplaceLiveDoctorRole()
    {
        var doctor = await Doctor([Roles.Nurse]); await using var api = await Api.Create(database.ConnectionString);
        using var response = await api.Get(doctor.User, roles: [Roles.Doctor]); Assert.Equal(403, (int)response.StatusCode);
    }

    [Fact]
    public async Task AdminPlusDoctorIsLimitedToOwnVerifiedSchedule()
    {
        var own = await Doctor([Roles.Admin, Roles.Doctor]); var other = await Doctor();
        var item = await Appointment(own.Staff, Now); await Appointment(other.Staff, Now);
        await using var api = await Api.Create(database.ConnectionString);
        using var response = await api.Get(own.User, roles: [Roles.Admin, Roles.Doctor]);
        Assert.Equal(item.Id.Value, Assert.Single((await response.Content.ReadFromJsonAsync<DoctorScheduleDto>())!.Appointments).AppointmentId);
    }

    [Fact]
    public async Task EmptyDayReturnsActualZerosAndNullWithoutHistoricalFallback()
    {
        var doctor = await Doctor(); await Appointment(doctor.Staff, Now.AddDays(-1)); await Appointment(doctor.Staff, Now.AddDays(1));
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User);
        var dto = (await response.Content.ReadFromJsonAsync<DoctorScheduleDto>())!;
        Assert.Empty(dto.Appointments); Assert.Null(dto.NextScheduledConsultation); Assert.Equal(new DoctorScheduleSummaryDto(0, 0, 0, 0, 0, 0), dto.Summary);
    }

    [Fact]
    public async Task AllRealStatusCountsOrderingAndEligibleNextAreAccurate()
    {
        var doctor = await Doctor();
        await Appointment(doctor.Staff, Now.AddHours(3));
        await Appointment(doctor.Staff, Now.AddHours(-1));
        var next = await Appointment(doctor.Staff, Now, AppointmentStatus.Confirmed);
        await Appointment(doctor.Staff, Now.AddMinutes(1), AppointmentStatus.Completed);
        await Appointment(doctor.Staff, Now.AddMinutes(2), AppointmentStatus.Cancelled);
        await Appointment(doctor.Staff, Now.AddMinutes(3), AppointmentStatus.NoShow);
        await Appointment(doctor.Staff, Now.AddMinutes(5)); await Appointment(doctor.Staff, Now.AddMinutes(5));
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User);
        var dto = (await response.Content.ReadFromJsonAsync<DoctorScheduleDto>())!;
        Assert.Equal(new DoctorScheduleSummaryDto(8, 4, 1, 1, 1, 1), dto.Summary);
        Assert.Equal(next.Id.Value, dto.NextScheduledConsultation!.AppointmentId);
        Assert.Equal(dto.Appointments.OrderBy(a => a.StartDateTimeUtc).Select(a => a.AppointmentId), dto.Appointments.Select(a => a.AppointmentId));
        // PostgreSQL UUID ordering is byte/lexical ordering, rather than Guid.CompareTo's signed field ordering.
        var tied = dto.Appointments.Where(a => a.StartDateTimeUtc == Now.AddMinutes(5)).Select(a => a.AppointmentId.ToString()).ToArray();
        Assert.Equal(tied.Order(StringComparer.Ordinal), tied);
    }

    [Theory] [InlineData(AppointmentStatus.Completed)] [InlineData(AppointmentStatus.Cancelled)] [InlineData(AppointmentStatus.NoShow)]
    public async Task TerminalStatusIsNeverNext(AppointmentStatus status)
    {
        var doctor = await Doctor(); await Appointment(doctor.Staff, Now.AddMinutes(1), status);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User);
        Assert.Null((await response.Content.ReadFromJsonAsync<DoctorScheduleDto>())!.NextScheduledConsultation);
    }

    [Theory] [InlineData(AppointmentStatus.Scheduled)] [InlineData(AppointmentStatus.Confirmed)]
    public async Task BothActiveStatusesQualifyAtCurrentInstantButPastOnesDoNot(AppointmentStatus status)
    {
        var doctor = await Doctor(); await Appointment(doctor.Staff, Now.AddTicks(-10), status);
        var current = await Appointment(doctor.Staff, Now, status);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User);
        Assert.Equal(current.Id.Value, (await response.Content.ReadFromJsonAsync<DoctorScheduleDto>())!.NextScheduledConsultation!.AppointmentId);
    }

    [Fact]
    public async Task PastActiveAppointmentsDoNotBecomeNext()
    {
        var doctor = await Doctor(); await Appointment(doctor.Staff, Now.AddHours(-1));
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User);
        Assert.Null((await response.Content.ReadFromJsonAsync<DoctorScheduleDto>())!.NextScheduledConsultation);
    }

    [Fact]
    public async Task LocalMidnightHalfOpenBoundariesExcludeAdjacentDaysAndSoftDeletes()
    {
        var doctor = await Doctor(); await Appointment(doctor.Staff, Start.AddTicks(-10));
        var first = await Appointment(doctor.Staff, Start); var last = await Appointment(doctor.Staff, Start.AddDays(1).AddTicks(-10));
        await Appointment(doctor.Staff, Start.AddDays(1)); await Appointment(doctor.Staff, Now, deleted: true);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User);
        var dto = (await response.Content.ReadFromJsonAsync<DoctorScheduleDto>())!;
        Assert.Equal(new[] { first.Id.Value, last.Id.Value }, dto.Appointments.Select(a => a.AppointmentId)); Assert.Equal(2, dto.Summary.Total);
    }

    [Fact]
    public async Task OmittedDateUsesFacilityDateThroughControllableClock()
    {
        var doctor = await Doctor(); var appointment = await Appointment(doctor.Staff, Start.AddHours(1));
        await using var api = await Api.Create(database.ConnectionString, Start.AddHours(2)); // UTC previous day, facility Oct 22.
        using var response = await api.Get(doctor.User, ""); var dto = (await response.Content.ReadFromJsonAsync<DoctorScheduleDto>())!;
        Assert.Equal(Day, dto.Date); Assert.Equal("Asia/Karachi", dto.TimeZoneId); Assert.Equal(appointment.Id.Value, Assert.Single(dto.Appointments).AppointmentId);
    }

    [Theory] [InlineData("2026-10-21")] [InlineData("2026-10-23")]
    public async Task ExplicitSelectedDateIsHonored(string date)
    {
        var doctor = await Doctor(); var day = DateOnly.Parse(date); var utc = new FacilityTimeZone("Asia/Karachi").GetUtcWindow(day).StartUtc;
        var item = await Appointment(doctor.Staff, utc); await Appointment(doctor.Staff, Now);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User, "?date=" + date);
        var dto = (await response.Content.ReadFromJsonAsync<DoctorScheduleDto>())!;
        Assert.Equal(day, dto.Date); Assert.Equal(item.Id.Value, Assert.Single(dto.Appointments).AppointmentId);
        Assert.Equal(day > Day ? item.Id.Value : (Guid?)null, dto.NextScheduledConsultation?.AppointmentId);
    }

    [Fact]
    public async Task ResponseContainsOnlyPurposeBuiltFieldsAndNoPrivateNotesOrIdentity()
    {
        var doctor = await Doctor(); await Appointment(doctor.Staff, Now);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "date", "timeZoneId", "doctor", "summary", "nextScheduledConsultation", "appointments" }, json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(new[] { "displayName", "specialty", "department" }, json.RootElement.GetProperty("doctor").EnumerateObject().Select(p => p.Name));
        var item = json.RootElement.GetProperty("appointments")[0];
        Assert.Equal(new[] { "appointmentId", "patientId", "patientMedicalRecordNumber", "patientDisplayName", "startDateTimeUtc", "endDateTimeUtc", "status", "reason" }, item.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Scheduled Patient", item.GetProperty("patientDisplayName").GetString());
        Assert.StartsWith("DS-", item.GetProperty("patientMedicalRecordNumber").GetString());
    }

    [Fact]
    public async Task DeletedPatientDoesNotHideAppointmentOrExposeDeletedIdentity()
    {
        var doctor = await Doctor(); await Appointment(doctor.Staff, Now, deletedPatient: true);
        await using var api = await Api.Create(database.ConnectionString); using var response = await api.Get(doctor.User);
        var item = Assert.Single((await response.Content.ReadFromJsonAsync<DoctorScheduleDto>())!.Appointments);
        Assert.Null(item.PatientDisplayName); Assert.Null(item.PatientMedicalRecordNumber);
    }

    [Theory] [InlineData("not-a-date")] [InlineData("2026-02-30")] [InlineData("9999-12-31")]
    public async Task InvalidDatesAreControlledBadRequests(string date)
    {
        var doctor = await Doctor(); await using var api = await Api.Create(database.ConnectionString);
        using var response = await api.Get(doctor.User, "?date=" + date); Assert.Equal(400, (int)response.StatusCode);
    }

    [Theory] [InlineData("2026-03-08", 23)] [InlineData("2026-11-01", 25)]
    public void FacilityWindowConvertsBothMidnightsIndependentlyAcrossDst(string date, int hours)
    {
        var (start, end) = new FacilityTimeZone("America/New_York").GetUtcWindow(DateOnly.Parse(date));
        Assert.Equal(hours, (end - start).TotalHours); Assert.Equal(DateTimeKind.Utc, start.Kind);
    }

    [Fact] public void KarachiWindowIsExplicitAndModelUnchanged()
    {
        var (start, end) = new FacilityTimeZone("Asia/Karachi").GetUtcWindow(Day);
        Assert.Equal(Start, start); Assert.Equal(Start.AddDays(1), end);
        using var db = database.Context(); Assert.False(db.Database.HasPendingModelChanges());
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(now); }
    private sealed class SqlCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
    private sealed class Api(WebApplication app, HttpClient client, JwtSettings settings, SqlCapture sql) : IAsyncDisposable
    {
        public SqlCapture Sql => sql;
        public static async Task<Api> Create(string connection, DateTime? now = null)
        {
            var settings = new JwtSettings { Issuer = "DoctorScheduleTests", Audience = "DoctorScheduleTests", Secret = "doctor-schedule-isolated-test-signing-secret-at-least-32-characters" };
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddApplication(); builder.Services.AddHttpContextAccessor();
            var sql = new SqlCapture();
            builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connection).AddInterceptors(sql));
            builder.Services.AddScoped<IApplicationDbContext>(p => p.GetRequiredService<ApplicationDbContext>());
            builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>(); builder.Services.AddScoped<ICurrentStaffService, CurrentStaffService>();
            builder.Services.AddScoped<IJwtSessionValidator, JwtSessionValidator>();
            builder.Services.AddSingleton<TimeProvider>(new FixedClock(now ?? Now)); builder.Services.AddSingleton(new FacilityTimeZone("Asia/Karachi"));
            builder.Services.AddAccountJwtAuthentication(settings); builder.Services.AddAuthorization();
            var app = builder.Build(); app.UseMiddleware<GlobalExceptionHandlingMiddleware>(); app.UseAuthentication(); app.UseAuthorization(); app.MapDoctorEndpoints();
            await app.StartAsync(); var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new HttpClient { BaseAddress = new Uri(address) }, settings, sql);
        }
        public async Task<HttpResponseMessage> Get(ApplicationUser user, string query = "?date=2026-10-22", string[]? roles = null, bool anonymous = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/doctor/schedule" + query);
            if (!anonymous) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtTokenGenerator(Options.Create(settings))
                .GenerateToken(user.Id.ToString(), user.Email!, user.FirstName, user.LastName, roles ?? [Roles.Doctor], user.SecurityStamp!));
            var response = await client.SendAsync(request);
            if (!anonymous && (roles is null || roles.Contains(Roles.Doctor)) && response.StatusCode == System.Net.HttpStatusCode.Conflict)
                throw new Xunit.Sdk.XunitException(await response.Content.ReadAsStringAsync());
            return response;
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
