using System.Collections.Concurrent;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientAccount;
using HealthERP.Domain.Patients;
using HealthERP.Infrastructure.Identity;
using HealthERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HealthERP.Api.Authentication;
using HealthERP.Api.Endpoints;
using HealthERP.Api.Middleware;
using HealthERP.Application;
using HealthERP.Application.Common.Configurations;
using HealthERP.Application.Common.Security;
using HealthERP.Application.PatientAccount.Models;
using HealthERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace HealthERP.Api.IntegrationTests;

[Collection("Pharmacy PostgreSQL")]
public class PatientActivationTests(PharmacyPostgresFixture database)
{
    private const string Code = "123456";
    private sealed record User(Guid Id) : ICurrentUserService
    { public string UserId => Id.ToString(); public string UserName => "activation-test"; }
    private sealed class Mail : IEmailService
    {
        public ConcurrentQueue<(string Email, string Code)> Sent { get; } = new();
        public bool Fail { get; set; }
        public Func<Task>? BeforeSend { get; set; }
        public Task SendPasswordResetEmailAsync(string e, string n, string t, CancellationToken ct = default) => throw new NotSupportedException();
        public async Task SendPatientActivationCodeEmailAsync(string e, string n, string c, CancellationToken ct = default)
        {
            if (BeforeSend is not null) await BeforeSend();
            Sent.Enqueue((e, c));
            if (Fail) throw new ActivationEmailDeliveryException(new Exception("Test delivery failure"));
        }
    }
    private async Task<(User User, Patient Patient)> Seed(string? email = "record@example.invalid")
    {
        await using var db = database.Context();
        var user = new User(Guid.NewGuid());
        db.Users.Add(new ApplicationUser { Id = user.Id, UserName = user.Id.ToString(), Email = "account@example.invalid",
            FirstName = "Activation", LastName = "Test", SecurityStamp = "unchanged-stamp", ConcurrencyStamp = "unchanged-concurrency",
            PasswordHash = "unchanged-password", IsActive = true, CreatedAtUtc = DateTime.UtcNow });
        var role = await db.Roles.SingleOrDefaultAsync(r => r.NormalizedName == "PATIENT");
        if (role is null) { role = new ApplicationRole(Roles.Patient) { Id = Guid.NewGuid(), NormalizedName = "PATIENT" }; db.Roles.Add(role); }
        db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
        var patient = Patient.Register("A-" + Guid.NewGuid().ToString("N")[..16], "Activation", "Test", new DateOnly(1990, 1, 1), email: email);
        patient.CreatedAtUtc = DateTime.UtcNow;
        db.Patients.Add(patient); await db.SaveChangesAsync(); return (user, patient);
    }
    private async Task<PatientActivationChallenge> Challenge(User user, Patient patient, int attempts = 0, bool consumed = false,
        bool cooldown = false, bool expired = false, string code = Code, int age = 0)
    {
        await using var db = database.Context();
        var c = PatientActivationChallenge.Create(user.Id, patient.Id, code,
            expired ? TimeSpan.FromMinutes(-1) : TimeSpan.FromMinutes(10), cooldown ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(-1));
        c.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-age);
        for (var i = 0; i < attempts; i++) c.VerifyCode("999999");
        if (consumed) c.MarkConsumed();
        db.PatientActivationChallenges.Add(c); await db.SaveChangesAsync(); return c;
    }
    private async Task<HealthERP.Application.PatientAccount.Models.StartActivationResponseDto> Start(User u, Patient p, Mail mail)
    { await using var db = database.Context(); return await new PatientActivationService(db, u, mail).StartAsync(p.MedicalRecordNumber, p.DateOfBirth); }
    private async Task<HealthERP.Application.PatientAccount.Models.VerifyActivationResponseDto> Verify(User u, string code = Code)
    { await using var db = database.Context(); return await new PatientActivationService(db, u, new Mail()).VerifyAsync(code); }
    private async Task<PatientActivationChallenge[]> History(User u)
    { await using var db = database.Context(); return await db.PatientActivationChallenges.Where(c => c.UserId == u.Id).OrderByDescending(c => c.CreatedAtUtc).ToArrayAsync(); }
    private async Task<Guid?> Link(Patient p)
    { await using var db = database.Context(); return (await db.Patients.SingleAsync(x => x.Id == p.Id)).UserId; }

    // External row lock lets every independent operation reach PostgreSQL before release.
    // There is no save barrier that waits for a request blocked at the user anchor.
    private async Task<T[]> Race<T>(Guid id, Func<Task<T>>[] operations, bool patient = false)
    {
        await using var blocker = new NpgsqlConnection(database.ConnectionString); await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        var table = patient ? "patients.patients" : "identity.users";
        await using var cmd = new NpgsqlCommand($"SELECT 1 FROM {table} WHERE \"Id\" = @id FOR UPDATE", blocker, tx);
        cmd.Parameters.AddWithValue("id", id); await cmd.ExecuteScalarAsync();
        var tasks = operations.Select(op => op()).ToArray();
        try
        {
            await using var observer = new NpgsqlConnection(database.ConnectionString); await observer.OpenAsync();
            await using var wait = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query ILIKE @pattern", observer);
            wait.Parameters.AddWithValue("pattern", patient ? "%patients%" : "%users%");
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (Convert.ToInt32(await wait.ExecuteScalarAsync()) < tasks.Length)
            { Assert.True(DateTime.UtcNow < deadline, "Independent operations did not reach the database lock."); await Task.Delay(25); }
        }
        finally { await tx.RollbackAsync(); }
        return await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Fact] public async Task NormalStart_UsesStoredRecipientAndStoresOnlyHash()
    {
        var (u,p) = await Seed(); var mail = new Mail(); var response = await Start(u,p,mail);
        Assert.True(response.Success); Assert.True(response.ResendCooldownUntilUtc > DateTime.UtcNow);
        var sent = Assert.Single(mail.Sent); Assert.Equal(p.Email, sent.Email);
        Assert.Equal(PatientActivationChallenge.HashCode(sent.Code), Assert.Single(await History(u)).CodeHash);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ConcurrentStartsAndReplacementResends_IssueOne(bool replacement)
    {
        var (u,p) = await Seed(); if (replacement) await Challenge(u,p);
        var mail = new Mail(); await Race(u.Id, [() => Start(u,p,mail), () => Start(u,p,mail)]);
        Assert.Single(mail.Sent); var rows = await History(u);
        Assert.Equal(replacement ? 2 : 1, rows.Length); Assert.Single(rows, c => !c.IsConsumed);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task PendingOrConsumedHistory_EnforcesCooldown(bool consumed)
    {
        var (u,p) = await Seed(); await Challenge(u,p,consumed:consumed,cooldown:true); var mail = new Mail();
        await Race(u.Id, [() => Start(u,p,mail), () => Start(u,p,mail)]);
        Assert.Empty(mail.Sent); Assert.Single(await History(u));
    }
    [Fact] public async Task ReplacementPermanentlySupersedesOlderCode()
    {
        var (u,p) = await Seed(); await Challenge(u,p); var mail = new Mail(); await Start(u,p,mail);
        Assert.False((await Verify(u)).Success); Assert.True((await Verify(u,Assert.Single(mail.Sent).Code)).Success);
        Assert.All(await History(u), c => Assert.True(c.IsConsumed));
    }
    [Theory] [InlineData(0,3,3,false)] [InlineData(4,3,5,true)] [InlineData(4,1,5,true)]
    public async Task WrongAttempts_AreAtomicAndFifthImmediatelyTerminal(int before,int requests,int count,bool terminal)
    {
        var (u,p) = await Seed(); await Challenge(u,p,attempts:before);
        var results = await Race(u.Id, Enumerable.Range(0,requests).Select(_ => (Func<Task<HealthERP.Application.PatientAccount.Models.VerifyActivationResponseDto>>)(() => Verify(u,"999999"))).ToArray());
        Assert.All(results, r => Assert.False(r.Success)); var c = Assert.Single(await History(u));
        Assert.Equal(count,c.AttemptCount); Assert.Equal(terminal,c.IsConsumed); Assert.Null(await Link(p));
    }
    [Fact] public async Task ConcurrentCorrectCode_ActivatesOnceAndReplayFails()
    {
        var (u,p) = await Seed(); await Challenge(u,p);
        var results = await Race(u.Id,[() => Verify(u), () => Verify(u)]);
        Assert.Single(results, r => r.Success); Assert.Equal(u.Id,await Link(p));
        Assert.Equal(1,Assert.Single(await History(u)).AttemptCount); Assert.False((await Verify(u)).Success);
    }
    [Fact] public async Task CorrectVersusWrong_HasConsistentCommittedState()
    {
        var (u,p) = await Seed(); await Challenge(u,p);
        var results = await Race(u.Id,[() => Verify(u), () => Verify(u,"999999")]);
        Assert.True(results[0].Success); Assert.False(results[1].Success); Assert.Equal(u.Id,await Link(p));
        var c = Assert.Single(await History(u)); Assert.True(c.IsConsumed); Assert.InRange(c.AttemptCount,1,2);
    }
    [Fact] public async Task VerifyVersusResend_CorrespondsToOneValidOrder()
    {
        var (u,p) = await Seed(); await Challenge(u,p); var mail = new Mail();
        var results = await Race<object>(u.Id,[async () => await Verify(u), async () => await Start(u,p,mail)]);
        var verified = (HealthERP.Application.PatientAccount.Models.VerifyActivationResponseDto)results[0];
        if (verified.Success) { Assert.Equal(u.Id,await Link(p)); Assert.Empty(mail.Sent); }
        else { Assert.Null(await Link(p)); Assert.Single(mail.Sent); Assert.True((await Verify(u,mail.Sent.Single().Code)).Success); }
    }
    [Fact] public async Task TwoAccountsCannotClaimOnePatient()
    {
        var (a,p) = await Seed(); var (b,_) = await Seed(); await Challenge(a,p); await Challenge(b,p);
        var results = await Race(p.Id.Value,[() => Verify(a), () => Verify(b)],patient:true);
        Assert.Single(results, r => r.Success); Assert.Contains(await Link(p),new Guid?[]{a.Id,b.Id});
        Assert.Single((await History(a)).Concat(await History(b)), c => c.IsConsumed);
    }
    [Fact] public async Task OneAccountCannotLinkTwoPatientsOrIssueTwoFlows()
    {
        var (u,p) = await Seed(); var (_,other) = await Seed(); var mail = new Mail();
        await Race(u.Id,[() => Start(u,p,mail),() => Start(u,other,mail)]); Assert.Single(mail.Sent);
        Assert.True((await Verify(u,mail.Sent.Single().Code)).Success);
        Assert.Equal(1,new[]{await Link(p),await Link(other)}.Count(id => id == u.Id));
        Assert.False((await Start(u,other,mail)).Success); Assert.Single(mail.Sent);
    }
    [Theory] [InlineData(true,false)] [InlineData(false,true)]
    public async Task ConsumedAndExpiredCodesAreRejected(bool consumed,bool expired)
    {
        var (u,p) = await Seed(); await Challenge(u,p,consumed:consumed,expired:expired);
        Assert.False((await Verify(u)).Success); Assert.Null(await Link(p)); Assert.True(Assert.Single(await History(u)).IsConsumed);
    }
    [Fact] public async Task LegacyOlderPendingCannotResurfaceBehindConsumedLatest()
    {
        var (u,p) = await Seed(); await Challenge(u,p,age:2); await Challenge(u,p,consumed:true,code:"234567");
        Assert.False((await Verify(u)).Success); Assert.All(await History(u),c => Assert.True(c.IsConsumed));
    }
    [Fact] public async Task MissingPatientEmailRequiresSupportWithoutIssuance()
    {
        var (u,p) = await Seed(null); var mail = new Mail(); var r = await Start(u,p,mail);
        Assert.False(r.Success); Assert.True(r.RequiresSupport); Assert.Empty(mail.Sent); Assert.Empty(await History(u));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task MrnOrDobMismatchIsGenericAndDoesNotIssue(bool mrn)
    {
        var (u,p) = await Seed(); var mail = new Mail(); await using var db = database.Context();
        var r = await new PatientActivationService(db,u,mail).StartAsync(mrn ? "missing" : p.MedicalRecordNumber,mrn ? p.DateOfBirth : p.DateOfBirth.AddDays(1));
        Assert.True(r.Success); Assert.Empty(mail.Sent); Assert.Empty(await History(u));
    }
    [Fact] public async Task EmailFailurePreservesIssuanceAndCooldownAndReleasesLocks()
    {
        var (u,p) = await Seed(); var mail = new Mail { Fail = true };
        mail.BeforeSend = async () => {
            await using var connection = new NpgsqlConnection(database.ConnectionString); await connection.OpenAsync();
            await using var tx = await connection.BeginTransactionAsync();
            await using var cmd = new NpgsqlCommand("SELECT 1 FROM identity.users WHERE \"Id\" = @id FOR UPDATE NOWAIT",connection,tx);
            cmd.Parameters.AddWithValue("id",u.Id); await cmd.ExecuteScalarAsync();
        };
        var r = await Start(u,p,mail); Assert.False(r.Success); Assert.True(r.ResendCooldownUntilUtc > DateTime.UtcNow);
        await Start(u,p,mail); Assert.Single(mail.Sent); Assert.Single(await History(u));
    }
    [Fact] public async Task IdentitySecurityAndAccountStateAreUnchanged()
    {
        var (u,p) = await Seed(); await Challenge(u,p); await Verify(u);
        await using var db = database.Context(); var identity = await db.Users.SingleAsync(x => x.Id == u.Id);
        Assert.Equal("unchanged-stamp",identity.SecurityStamp); Assert.Equal("unchanged-concurrency",identity.ConcurrencyStamp);
        Assert.Equal("unchanged-password",identity.PasswordHash); Assert.True(identity.IsActive); Assert.False(identity.EmailConfirmed);
        Assert.Equal("account@example.invalid",identity.Email);
        var roleId = Assert.Single(await db.UserRoles.Where(r => r.UserId == u.Id).ToArrayAsync()).RoleId;
        Assert.Equal(Roles.Patient,(await db.Roles.SingleAsync(r => r.Id == roleId)).Name);
    }
    [Fact] public async Task LinkFailureRollsBackConsumptionAndAttempts()
    {
        var (u,p) = await Seed(); await Challenge(u,p);
        var suffix = Guid.NewGuid().ToString("N"); var function = "activation_fail_" + suffix;
        await using var db = database.Context();
        // DDL identifiers contain only a fixed prefix and internally generated GUID hex.
        var createSql = $"CREATE FUNCTION patients.{function}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Id\" = '{p.Id.Value}'::uuid THEN RAISE EXCEPTION 'private activation failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER {function} BEFORE UPDATE ON patients.patients FOR EACH ROW EXECUTE FUNCTION patients.{function}();";
        await db.Database.ExecuteSqlRawAsync(createSql);
        try {
            await Assert.ThrowsAsync<ActivationStorageException>(() => new PatientActivationService(db,u,new Mail()).VerifyAsync(Code));
            Assert.Empty(db.ChangeTracker.Entries()); Assert.Null(await Link(p));
            var c = Assert.Single(await History(u)); Assert.False(c.IsConsumed); Assert.Equal(0,c.AttemptCount);
        }
        finally { var dropSql = $"DROP TRIGGER {function} ON patients.patients; DROP FUNCTION patients.{function}();"; await db.Database.ExecuteSqlRawAsync(dropSql); }
    }
    [Fact] public void ActivationRequiresNoModelMigration()
    { using var db = database.Context(); Assert.False(db.Database.HasPendingModelChanges()); }

    [Fact] public async Task CorrectCodeAtLastComparisonSucceeds_ButExhaustedCodeCannotCompareAgain()
    {
        var (u,p) = await Seed(); await Challenge(u,p,attempts:4);
        Assert.True((await Verify(u)).Success); Assert.Equal(5,Assert.Single(await History(u)).AttemptCount);
        var (v,q) = await Seed(); await Challenge(v,q,attempts:5,cooldown:true);
        Assert.False((await Verify(v)).Success); Assert.Null(await Link(q));
        var mail = new Mail(); await Start(v,q,mail); Assert.Empty(mail.Sent);
        Assert.Equal(5,Assert.Single(await History(v)).AttemptCount);
    }

    [Fact] public async Task IndependentConcurrentHttpStartsIssueOneEmail()
    {
        var (u,p) = await Seed(); var mail = new Mail(); await using var host = await ApiHost.Create(database.ConnectionString,mail);
        var body = new { medicalRecordNumber=p.MedicalRecordNumber,dateOfBirth=p.DateOfBirth };
        var responses = await Race(u.Id,[() => host.Request(u,"/activation/start",body),() => host.Request(u,"/activation/start",body)]);
        foreach(var response in responses) { using(response) { Assert.Equal(200,(int)response.StatusCode); Assert.True((await response.Content.ReadFromJsonAsync<StartActivationResponseDto>())!.Success); } }
        Assert.Single(mail.Sent); Assert.Single(await History(u));
    }

    [Theory] [InlineData("P0001",500)] [InlineData("23505",409)] [InlineData("40001",409)] [InlineData("40P01",409)]
    public async Task DatabaseLinkFailuresAreControlledAndAtomicOverHttp(string sqlState,int status)
    {
        var (u,p) = await Seed(); await Challenge(u,p); var function="activation_http_fail_"+Guid.NewGuid().ToString("N");
        await using var db=database.Context();
        // All DDL values are internal GUIDs or the fixed theory's SQLSTATE constants.
        var create=$"CREATE FUNCTION patients.{function}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Id\" = '{p.Id.Value}'::uuid THEN RAISE EXCEPTION 'private activation failure' USING ERRCODE = '{sqlState}'; END IF; RETURN NEW; END $$; CREATE TRIGGER {function} BEFORE UPDATE ON patients.patients FOR EACH ROW EXECUTE FUNCTION patients.{function}();";
        await db.Database.ExecuteSqlRawAsync(create);
        try {
            await using var host=await ApiHost.Create(database.ConnectionString,new Mail());
            using var response=await host.Request(u,"/activation/verify",new { code=Code });
            Assert.Equal(status,(int)response.StatusCode); var json=await response.Content.ReadAsStringAsync();
            Assert.Contains("detail",json); Assert.DoesNotContain("private activation failure",json); Assert.DoesNotContain("Npgsql",json);
            Assert.Null(await Link(p)); var c=Assert.Single(await History(u)); Assert.False(c.IsConsumed); Assert.Equal(0,c.AttemptCount);
        }
        finally { var drop=$"DROP TRIGGER {function} ON patients.patients; DROP FUNCTION patients.{function}();"; await db.Database.ExecuteSqlRawAsync(drop); }
    }

    [Fact] public async Task HttpContractIgnoresInjectedIdsAndEmail_AndStatusReflectsPersistedLink()
    {
        var (u,p) = await Seed(); var (other,wrongPatient) = await Seed(); var mail = new Mail();
        await using var host = await ApiHost.Create(database.ConnectionString,mail);
        using var started = await host.Request(u,"/activation/start",new { medicalRecordNumber=p.MedicalRecordNumber,dateOfBirth=p.DateOfBirth,
            userId=other.Id,patientId=wrongPatient.Id.Value,email="injected@example.invalid" });
        Assert.Equal(200,(int)started.StatusCode); Assert.True((await started.Content.ReadFromJsonAsync<StartActivationResponseDto>())!.Success);
        var issued = Assert.Single(await History(u)); Assert.Equal(p.Id,issued.PatientId); Assert.Empty(await History(other));
        Assert.Equal(p.Email,Assert.Single(mail.Sent).Email);
        using var verified = await host.Request(u,"/activation/verify",new { code=mail.Sent.Single().Code,userId=other.Id,patientId=wrongPatient.Id.Value });
        Assert.True((await verified.Content.ReadFromJsonAsync<VerifyActivationResponseDto>())!.Success);
        using var status = await host.Request(u,"/status");
        var state = (await status.Content.ReadFromJsonAsync<PatientAccountStatusDto>())!;
        Assert.True(state.IsLinked); Assert.Equal(p.MedicalRecordNumber,state.Patient!.Mrn);
        Assert.Null(await Link(wrongPatient));
    }
    [Theory] [InlineData(null,401)] [InlineData(Roles.Doctor,403)]
    public async Task HttpAuthorizationRemainsEnforced(string? role,int expected)
    {
        var (u,p) = await Seed(); var mail = new Mail(); await using var host = await ApiHost.Create(database.ConnectionString,mail);
        using var response = await host.Request(u,"/activation/start",new { medicalRecordNumber=p.MedicalRecordNumber,dateOfBirth=p.DateOfBirth },role);
        Assert.Equal(expected,(int)response.StatusCode); Assert.Empty(await History(u)); Assert.Empty(mail.Sent);
    }
    private sealed class ApiHost(WebApplication app,HttpClient client,JwtSettings settings) : IAsyncDisposable
    {
        public static async Task<ApiHost> Create(string connection,Mail mail)
        {
            var settings = new JwtSettings { Issuer="ActivationTests",Audience="ActivationTests",Secret="isolated-activation-tests-secret-at-least-32-characters" };
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddApplication(); builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connection));
            builder.Services.AddScoped<IApplicationDbContext>(p => p.GetRequiredService<ApplicationDbContext>());
            builder.Services.AddScoped<ICurrentUserService,CurrentUserService>();
            builder.Services.AddScoped<ICurrentPatientService,CurrentPatientService>();
            builder.Services.AddScoped<IPatientActivationService,PatientActivationService>(); builder.Services.AddSingleton<IEmailService>(mail);
            var sessions = new Mock<IJwtSessionValidator>();
            sessions.Setup(s => s.ValidateAsync(It.IsAny<Guid>(),It.IsAny<string?>(),It.IsAny<CancellationToken>())).ReturnsAsync(true);
            builder.Services.AddSingleton(sessions.Object); builder.Services.AddAccountJwtAuthentication(settings); builder.Services.AddAuthorization();
            var app = builder.Build(); app.UseMiddleware<GlobalExceptionHandlingMiddleware>(); app.UseAuthentication(); app.UseAuthorization();
            app.MapPatientAccountEndpoints(); await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app,new HttpClient { BaseAddress=new Uri(address) },settings);
        }
        public async Task<HttpResponseMessage> Request(User user,string path,object? body=null,string? role=Roles.Patient)
        {
            using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post,"/api/v1/patient-account"+path);
            if (body is not null) request.Content=JsonContent.Create(body);
            if (role is not null) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",new JwtTokenGenerator(Options.Create(settings))
                .GenerateToken(user.UserId,"account@example.invalid","Test","User",[role],"unchanged-stamp"));
            return await client.SendAsync(request);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
