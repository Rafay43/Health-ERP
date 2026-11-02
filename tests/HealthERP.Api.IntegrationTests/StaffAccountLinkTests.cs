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
using HealthERP.Application.StaffMembers.Models;
using HealthERP.Domain.StaffMembers;
using HealthERP.Infrastructure.Identity;
using HealthERP.Infrastructure.Persistence;
using HealthERP.Infrastructure.Persistence.Interceptors;
using HealthERP.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Staff = HealthERP.Domain.StaffMembers.StaffMembers;

namespace HealthERP.Api.IntegrationTests;

[Collection("Pharmacy PostgreSQL")]
public class StaffAccountLinkTests(PharmacyPostgresFixture database)
{
    private async Task<ApplicationUser> Account(params string[] roles)
    {
        await using var db=database.Context(); var id=Guid.NewGuid(); var email=id.ToString("N")+"@example.invalid";
        var user=new ApplicationUser { Id=id,UserName=email,NormalizedUserName=email.ToUpperInvariant(),Email=email,NormalizedEmail=email.ToUpperInvariant(),
            FirstName="Account",LastName=id.ToString("N"),IsActive=true,SecurityStamp=Guid.NewGuid().ToString("N"),ConcurrencyStamp=Guid.NewGuid().ToString("N"),CreatedAtUtc=DateTime.UtcNow };
        db.Users.Add(user);
        foreach(var name in roles) {
            var role=await db.Roles.SingleOrDefaultAsync(r=>r.NormalizedName==name.ToUpperInvariant());
            if(role is null) { role=new ApplicationRole(name) { Id=Guid.NewGuid(),NormalizedName=name.ToUpperInvariant() }; db.Roles.Add(role); }
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId=id,RoleId=role.Id });
        }
        await db.SaveChangesAsync(); return user;
    }
    private async Task<Staff> Record(bool inactive=false,bool deleted=false,ApplicationUser? linked=null,StaffKind kind=StaffKind.Unspecified)
    {
        await using var db=database.Context(); var staff=Staff.Create("Recorded","Staff","staff@example.invalid","","General","","Outpatient");
        staff.CreatedAtUtc=DateTime.UtcNow; staff.CreatedBy="original-creator";
        if(inactive) staff.Deactivate(); if(linked is not null) staff.LinkUserId(linked.Id); staff.AssignStaffKind(kind);
        db.Staff.Add(staff); if(deleted) db.Entry(staff).Property(s=>s.IsDeleted).CurrentValue=true;
        await db.SaveChangesAsync(); return staff;
    }
    private async Task<Staff> Fresh(Staff staff)
    { await using var db=database.Context(); return await db.Staff.IgnoreQueryFilters().AsNoTracking().SingleAsync(s=>s.Id==staff.Id); }
    private async Task<ApplicationUser> Fresh(ApplicationUser user)
    { await using var db=database.Context(); return await db.Users.AsNoTracking().SingleAsync(u=>u.Id==user.Id); }
    private async Task<Api> Host() => await Api.Create(database.ConnectionString,await Account(Roles.Admin));

    [Fact] public async Task AdminLinkCommitsDoctorAssociationAuditAndSessionRevocation()
    {
        var user=await Account(Roles.Doctor); var staff=await Record(); await using var api=await Host();
        using var response=await api.Link(staff,user); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var dto=(await response.Content.ReadFromJsonAsync<StaffAccountLinkDto>())!; Assert.Equal(user.Id,dto.LinkedAccount!.UserId); Assert.Equal("Doctor",dto.StaffKind);
        var saved=await Fresh(staff); Assert.Equal(user.Id,saved.UserId); Assert.Equal(StaffKind.Doctor,saved.StaffKind);
        Assert.Equal(api.Admin.Id.ToString(),saved.ModifiedBy); Assert.Equal("original-creator",saved.CreatedBy); Assert.NotNull(saved.ModifiedAtUtc);
        Assert.NotEqual(user.SecurityStamp,(await Fresh(user)).SecurityStamp);
        using var stale=await api.Request(user,HttpMethod.Get,"/test/session",roles:[Roles.Doctor]); Assert.Equal(401,(int)stale.StatusCode);
        var fresh=await Fresh(user);
        using var staffResult=await api.Request(fresh,HttpMethod.Get,"/test/current-staff",roles:[Roles.Doctor]); Assert.Equal(200,(int)staffResult.StatusCode);
        using var doctorResult=await api.Request(fresh,HttpMethod.Get,"/test/current-doctor",roles:[Roles.Doctor]); Assert.Equal(200,(int)doctorResult.StatusCode);
        Assert.Equal(staff.Id,(await doctorResult.Content.ReadFromJsonAsync<CurrentStaffContext>())!.StaffId);
        using var list=await api.Request(api.Admin,HttpMethod.Get,"/api/v1/staff");
        Assert.Equal(user.Id,(await list.Content.ReadFromJsonAsync<List<StaffDto>>())!.Single(s=>s.Id==staff.Id.Value).LinkedAccount!.UserId);
    }
    [Theory] [InlineData(Roles.Doctor)] [InlineData(Roles.Nurse)] [InlineData(Roles.Receptionist)] [InlineData(Roles.Patient)] [InlineData(Roles.Pharmacist)] [InlineData(null)]
    public async Task AllAccountEndpointsIndependentlyRejectNonAdmin(string? role)
    {
        var caller=await Account(role is null ? [] : [role]); var target=await Account(Roles.Doctor); var staff=await Record(); await using var api=await Host();
        var expected=role is null ? 401 : 403;
        foreach(var operation in new[] { (HttpMethod.Post,$"/api/v1/staff/{staff.Id.Value}/account-link",(object?)new { userId=target.Id,staffKind=1 }),
            (HttpMethod.Delete,$"/api/v1/staff/{staff.Id.Value}/account-link",(object?)null),
            (HttpMethod.Get,"/api/v1/staff/account-lookup",(object?)null) }) {
            using var response=await api.Request(caller,operation.Item1,operation.Item2,operation.Item3,role is null ? [] : [role],authenticated:role is not null);
            Assert.Equal(expected,(int)response.StatusCode);
        }
        Assert.Null((await Fresh(staff)).UserId); Assert.Equal(target.SecurityStamp,(await Fresh(target)).SecurityStamp);
    }
    [Theory] [InlineData("missing-staff",404)] [InlineData("deleted-staff",404)] [InlineData("inactive-staff",409)]
    [InlineData("missing-account",404)] [InlineData("inactive-account",409)] [InlineData("locked-account",409)] [InlineData("wrong-role",409)]
    [InlineData("empty-account",400)] [InlineData("invalid-kind",400)]
    public async Task IneligibleLinkFailsWithoutAssociationOrStampChange(string scenario,int expected)
    {
        var user=await Account(scenario=="wrong-role" ? Roles.Nurse : Roles.Doctor);
        var staff=await Record(inactive:scenario=="inactive-staff",deleted:scenario=="deleted-staff");
        if(scenario is "inactive-account" or "locked-account") { await using var change=database.Context(); var u=await change.Users.SingleAsync(u=>u.Id==user.Id);
            if(scenario=="inactive-account") u.IsActive=false; else { u.LockoutEnabled=true; u.LockoutEnd=DateTimeOffset.UtcNow.AddMinutes(10); } await change.SaveChangesAsync(); }
        await using var api=await Host();
        using var response=await api.Request(api.Admin,HttpMethod.Post,$"/api/v1/staff/{(scenario=="missing-staff" ? Guid.NewGuid() : staff.Id.Value)}/account-link",
            new { userId=scenario=="missing-account" ? Guid.NewGuid() : scenario=="empty-account" ? Guid.Empty : user.Id,staffKind=scenario=="invalid-kind" ? 42 : 1 });
        Assert.Equal(expected,(int)response.StatusCode); Assert.Null((await Fresh(staff)).UserId); Assert.Equal(user.SecurityStamp,(await Fresh(user)).SecurityStamp);
    }
    [Fact] public async Task InvalidGuidJsonIsRejected()
    {
        var staff=await Record(); await using var api=await Host();
        using var response=await api.Request(api.Admin,HttpMethod.Post,$"/api/v1/staff/{staff.Id.Value}/account-link",new { userId="not-a-guid",staffKind=1 });
        Assert.Equal(400,(int)response.StatusCode); Assert.Null((await Fresh(staff)).UserId);
    }
    [Fact] public async Task ClassificationMustBeExplicitInRequest()
    {
        var user=await Account(Roles.Doctor); var staff=await Record(); await using var api=await Host();
        using var response=await api.Request(api.Admin,HttpMethod.Post,$"/api/v1/staff/{staff.Id.Value}/account-link",new { userId=user.Id });
        Assert.Equal(400,(int)response.StatusCode); Assert.Null((await Fresh(staff)).UserId);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task AlreadyLinkedAccountIncludingDeletedOwnerCannotBeClaimed(bool deleted)
    {
        var user=await Account(Roles.Doctor); await Record(linked:user,kind:StaffKind.Doctor,deleted:deleted); var staff=await Record(); await using var api=await Host();
        using var response=await api.Link(staff,user); Assert.Equal(409,(int)response.StatusCode); Assert.Null((await Fresh(staff)).UserId);
    }
    [Fact] public async Task DifferentAccountCannotSilentlyReplaceAssociation()
    {
        var owner=await Account(Roles.Doctor); var other=await Account(Roles.Doctor); var staff=await Record(linked:owner,kind:StaffKind.Doctor); await using var api=await Host();
        using var response=await api.Link(staff,other); Assert.Equal(409,(int)response.StatusCode);
        Assert.Equal(owner.Id,(await Fresh(staff)).UserId); Assert.Equal(other.SecurityStamp,(await Fresh(other)).SecurityStamp);
    }
    [Fact] public async Task ExactRepeatedLinkIsNoOpAndClassificationChangeRevokes()
    {
        var user=await Account(Roles.Doctor); var staff=await Record(); await using var api=await Host();
        using var first=await api.Link(staff,user); Assert.Equal(200,(int)first.StatusCode); var before=await Fresh(user); var recorded=await Fresh(staff);
        using var repeated=await api.Link(staff,user); Assert.Equal(200,(int)repeated.StatusCode);
        Assert.Equal(before.SecurityStamp,(await Fresh(user)).SecurityStamp); Assert.Equal(recorded.ModifiedAtUtc,(await Fresh(staff)).ModifiedAtUtc);
        using var changed=await api.Link(staff,user,StaffKind.Unspecified); Assert.Equal(200,(int)changed.StatusCode);
        Assert.NotEqual(before.SecurityStamp,(await Fresh(user)).SecurityStamp); Assert.Equal(StaffKind.Unspecified,(await Fresh(staff)).StaffKind);
    }
    [Fact] public async Task UnlinkResetsKindKeepsRecordsRevokesSessionAndResolverFails()
    {
        var user=await Account(Roles.Doctor); var staff=await Record(); await using var api=await Host();
        using var link=await api.Link(staff,user); Assert.Equal(200,(int)link.StatusCode); var afterLink=await Fresh(user);
        using var unlink=await api.Unlink(staff); Assert.Equal(204,(int)unlink.StatusCode);
        var saved=await Fresh(staff); Assert.Null(saved.UserId); Assert.Equal(StaffKind.Unspecified,saved.StaffKind); Assert.Equal(api.Admin.Id.ToString(),saved.ModifiedBy);
        var afterUnlink=await Fresh(user); Assert.NotEqual(afterLink.SecurityStamp,afterUnlink.SecurityStamp);
        using var old=await api.Request(afterLink,HttpMethod.Get,"/test/session",roles:[Roles.Doctor]); Assert.Equal(401,(int)old.StatusCode);
        using var resolver=await api.Request(afterUnlink,HttpMethod.Get,"/test/current-doctor",roles:[Roles.Doctor]); Assert.Equal(403,(int)resolver.StatusCode);
        using var repeated=await api.Unlink(staff); Assert.Equal(204,(int)repeated.StatusCode); Assert.Equal(afterUnlink.SecurityStamp,(await Fresh(user)).SecurityStamp);
    }
    [Fact] public async Task InactiveStaffAndAccountCanBeExplicitlyUnlinkedForCleanup()
    {
        var user=await Account(Roles.Doctor); var staff=await Record(inactive:true,linked:user,kind:StaffKind.Doctor);
        await using(var change=database.Context()) { (await change.Users.SingleAsync(u=>u.Id==user.Id)).IsActive=false; await change.SaveChangesAsync(); }
        await using var api=await Host(); using var response=await api.Unlink(staff); Assert.Equal(204,(int)response.StatusCode);
        Assert.Null((await Fresh(staff)).UserId); Assert.False((await Fresh(user)).IsActive);
    }
    [Fact] public async Task ExplicitIdsWinWithoutEmailNameMatchingOrRoleAssignment()
    {
        var intended=await Account(Roles.Nurse); var matching=await Account(Roles.Doctor); var staff=await Record();
        await using(var db=database.Context()) { var s=await db.Staff.SingleAsync(s=>s.Id==staff.Id); db.Entry(s).Property(x=>x.Email).CurrentValue=matching.Email!; await db.SaveChangesAsync(); }
        await using var api=await Host(); using var response=await api.Link(staff,intended,StaffKind.Unspecified); Assert.Equal(200,(int)response.StatusCode);
        Assert.Equal(intended.Id,(await Fresh(staff)).UserId); Assert.Equal(matching.SecurityStamp,(await Fresh(matching)).SecurityStamp);
        await using var fresh=database.Context(); var roles=await fresh.UserRoles.Join(fresh.Roles,m=>m.RoleId,r=>r.Id,(m,r)=>new {m.UserId,r.Name}).Where(r=>r.UserId==intended.Id).Select(r=>r.Name).ToArrayAsync();
        Assert.Equal(new[]{Roles.Nurse},roles);
    }
    [Fact] public async Task AccountLookupIsPagedSafeAndMarksInactiveAndHiddenLinks()
    {
        var a=await Account(Roles.Doctor); var b=await Account(Roles.Nurse); await Record(linked:a,deleted:true);
        await using(var change=database.Context()) { (await change.Users.SingleAsync(u=>u.Id==b.Id)).IsActive=false; await change.SaveChangesAsync(); }
        await using var api=await Host(); using var response=await api.Request(api.Admin,HttpMethod.Get,$"/api/v1/staff/account-lookup?search={Uri.EscapeDataString(a.Email!)}&pageSize=1");
        Assert.Equal(200,(int)response.StatusCode); var lookup=(await response.Content.ReadFromJsonAsync<StaffAccountLookupDto>())!;
        Assert.Equal(a.Id,Assert.Single(lookup.Items).UserId); Assert.True(lookup.Items[0].IsLinkedToStaff); Assert.Contains(Roles.Doctor,lookup.Items[0].Roles);
        var json=await response.Content.ReadAsStringAsync(); foreach(var field in new[]{"passwordHash","securityStamp","concurrencyStamp","lockoutEnd","token","userName"}) Assert.DoesNotContain(field,json);
        using var inactive=await api.Request(api.Admin,HttpMethod.Get,$"/api/v1/staff/account-lookup?search={Uri.EscapeDataString(b.Email!)}");
        Assert.False(Assert.Single((await inactive.Content.ReadFromJsonAsync<StaffAccountLookupDto>())!.Items).IsActive);
        using var page=await api.Request(api.Admin,HttpMethod.Get,"/api/v1/staff/account-lookup?pageSize=1");
        var paged=(await page.Content.ReadFromJsonAsync<StaffAccountLookupDto>())!; Assert.Single(paged.Items); Assert.True(paged.HasMore);
    }
    [Theory] [InlineData("page=0")] [InlineData("pageSize=51")] [InlineData("pageSize=0")]
    public async Task InvalidLookupBoundsAreRejected(string parameters)
    { await using var api=await Host(); using var response=await api.Request(api.Admin,HttpMethod.Get,"/api/v1/staff/account-lookup?"+parameters); Assert.Equal(400,(int)response.StatusCode); }

    private async Task<HttpResponseMessage[]> Race(Guid id,bool staffLock,Func<Task<HttpResponseMessage>> a,Func<Task<HttpResponseMessage>> b)
    {
        await using var connection=new NpgsqlConnection(database.ConnectionString); await connection.OpenAsync(); await using var tx=await connection.BeginTransactionAsync();
        await using var row=new NpgsqlCommand($"SELECT 1 FROM {(staffLock ? "staff.staff" : "identity.users")} WHERE \"Id\"=@id FOR UPDATE",connection,tx);
        row.Parameters.AddWithValue("id",id); await row.ExecuteScalarAsync(); var tasks=new[]{a(),b()};
        try {
            await using var observer=new NpgsqlConnection(database.ConnectionString); await observer.OpenAsync();
            await using var waiting=new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query ILIKE @pattern",observer);
            waiting.Parameters.AddWithValue("pattern",staffLock ? "%staff%" : "%users%"); var deadline=DateTime.UtcNow.AddSeconds(20);
            while(Convert.ToInt32(await waiting.ExecuteScalarAsync())<2) { Assert.True(DateTime.UtcNow<deadline,"Both independent requests must reach the PostgreSQL row lock."); await Task.Delay(25); }
        }
        finally { await tx.RollbackAsync(); }
        return await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
    }
    [Fact] public async Task ConcurrentSameAccountDifferentStaffCreatesExactlyOneAssociation()
    {
        var user=await Account(Roles.Doctor); var a=await Record(); var b=await Record(); await using var api=await Host();
        var responses=await Race(user.Id,false,()=>api.Link(a,user),()=>api.Link(b,user));
        Assert.Single(responses,r=>(int)r.StatusCode==200); Assert.Single(responses,r=>(int)r.StatusCode==409); foreach(var r in responses) r.Dispose();
        Assert.Equal(1,new[]{await Fresh(a),await Fresh(b)}.Count(s=>s.UserId==user.Id));
    }
    [Fact] public async Task ConcurrentDifferentAccountsSameStaffCannotOverwriteWinner()
    {
        var a=await Account(Roles.Doctor); var b=await Account(Roles.Doctor); var staff=await Record(); await using var api=await Host();
        var responses=await Race(staff.Id.Value,true,()=>api.Link(staff,a),()=>api.Link(staff,b));
        Assert.Single(responses,r=>(int)r.StatusCode==200); Assert.Single(responses,r=>(int)r.StatusCode==409); foreach(var r in responses) r.Dispose();
        var owner=(await Fresh(staff)).UserId; Assert.Contains(owner,new Guid?[]{a.Id,b.Id});
        Assert.Equal(owner==a.Id,b.SecurityStamp==(await Fresh(b)).SecurityStamp);
        Assert.Equal(owner==b.Id,a.SecurityStamp==(await Fresh(a)).SecurityStamp);
    }
    [Fact] public async Task UnlinkVersusExplicitNewLinkHasOneValidSerialOrder()
    {
        var old=await Account(Roles.Doctor); var next=await Account(Roles.Doctor); var staff=await Record(linked:old,kind:StaffKind.Doctor); await using var api=await Host();
        var responses=await Race(staff.Id.Value,true,()=>api.Unlink(staff),()=>api.Link(staff,next));
        Assert.Equal(204,(int)responses[0].StatusCode);
        var state=await Fresh(staff);
        if((int)responses[1].StatusCode==200) { Assert.Equal(next.Id,state.UserId); Assert.Equal(StaffKind.Doctor,state.StaffKind); Assert.NotEqual(next.SecurityStamp,(await Fresh(next)).SecurityStamp); }
        else { Assert.Equal(409,(int)responses[1].StatusCode); Assert.Null(state.UserId); Assert.Equal(StaffKind.Unspecified,state.StaffKind); Assert.Equal(next.SecurityStamp,(await Fresh(next)).SecurityStamp); }
        Assert.NotEqual(old.SecurityStamp,(await Fresh(old)).SecurityStamp); foreach(var r in responses) r.Dispose();
    }
    [Fact] public async Task AdminLinkingItsOwnAccountRevokesThatSessionAfterSuccessfulCommit()
    {
        var staff=await Record(); await using var api=await Host();
        using var response=await api.Link(staff,api.Admin,StaffKind.Unspecified); Assert.Equal(200,(int)response.StatusCode);
        Assert.Equal(api.Admin.Id,(await Fresh(staff)).UserId);
        using var stale=await api.Request(api.Admin,HttpMethod.Get,"/api/v1/staff/account-lookup"); Assert.Equal(401,(int)stale.StatusCode);
        using var fresh=await api.Request(await Fresh(api.Admin),HttpMethod.Get,"/api/v1/staff/account-lookup"); Assert.Equal(200,(int)fresh.StatusCode);
    }
    [Fact] public async Task FailedUnlinkPreservesAssociationClassificationAndExistingSession()
    {
        var user=await Account(Roles.Doctor); var staff=await Record(linked:user,kind:StaffKind.Doctor); var function="staff_unlink_fail_"+Guid.NewGuid().ToString("N");
        await using var db=database.Context();
        var create=$"CREATE FUNCTION identity.{function}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Id\"='{user.Id}'::uuid THEN RAISE EXCEPTION 'private unlink stamp failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER {function} BEFORE UPDATE ON identity.users FOR EACH ROW EXECUTE FUNCTION identity.{function}();";
        await db.Database.ExecuteSqlRawAsync(create);
        try {
            await using var api=await Host(); using var response=await api.Unlink(staff); Assert.Equal(500,(int)response.StatusCode);
            Assert.DoesNotContain("private unlink stamp failure",await response.Content.ReadAsStringAsync());
            var state=await Fresh(staff); Assert.Equal(user.Id,state.UserId); Assert.Equal(StaffKind.Doctor,state.StaffKind); Assert.Null(state.ModifiedAtUtc);
            Assert.Equal(user.SecurityStamp,(await Fresh(user)).SecurityStamp);
            using var session=await api.Request(user,HttpMethod.Get,"/test/session",roles:[Roles.Doctor]); Assert.Equal(204,(int)session.StatusCode);
        }
        finally { var drop=$"DROP TRIGGER {function} ON identity.users; DROP FUNCTION identity.{function}();"; await db.Database.ExecuteSqlRawAsync(drop); }
    }
    [Theory] [InlineData("users","P0001",500)] [InlineData("staff","P0001",500)] [InlineData("users","23505",409)]
    [InlineData("users","40001",409)] [InlineData("users","40P01",409)]
    public async Task WriteFailureRollsBackAssociationStampAndAudit(string table,string sqlState,int expected)
    {
        var user=await Account(Roles.Doctor); var staff=await Record(); var function="staff_link_fail_"+Guid.NewGuid().ToString("N");
        var schema=table=="users" ? "identity" : "staff"; var id=table=="users" ? user.Id : staff.Id.Value;
        await using var db=database.Context();
        // DDL names/values are internally generated GUIDs and fixed theory constants.
        var create=$"CREATE FUNCTION {schema}.{function}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Id\"='{id}'::uuid THEN RAISE EXCEPTION 'private stamp or staff failure' USING ERRCODE='{sqlState}'; END IF; RETURN NEW; END $$; CREATE TRIGGER {function} BEFORE UPDATE ON {schema}.{table} FOR EACH ROW EXECUTE FUNCTION {schema}.{function}();";
        await db.Database.ExecuteSqlRawAsync(create);
        try {
            await using var api=await Host(); using var response=await api.Link(staff,user); Assert.Equal(expected,(int)response.StatusCode);
            var json=await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("private stamp or staff failure",json); Assert.DoesNotContain("Npgsql",json);
            var saved=await Fresh(staff); Assert.Null(saved.UserId); Assert.Equal(StaffKind.Unspecified,saved.StaffKind); Assert.Null(saved.ModifiedAtUtc);
            Assert.Equal(user.SecurityStamp,(await Fresh(user)).SecurityStamp);
            using var session=await api.Request(user,HttpMethod.Get,"/test/session",roles:[Roles.Doctor]); Assert.Equal(204,(int)session.StatusCode);
        }
        finally { var drop=$"DROP TRIGGER {function} ON {schema}.{table}; DROP FUNCTION {schema}.{function}();"; await db.Database.ExecuteSqlRawAsync(drop); }
    }
    [Fact] public void ModelRequiresNoNewMigration()
    { using var db=database.Context(); Assert.False(db.Database.HasPendingModelChanges()); }

    private sealed class Api(WebApplication app,HttpClient client,JwtSettings settings,ApplicationUser admin) : IAsyncDisposable
    {
        public ApplicationUser Admin=>admin;
        public static async Task<Api> Create(string connection,ApplicationUser admin)
        {
            var settings=new JwtSettings { Issuer="StaffLinkTests",Audience="StaffLinkTests",Secret="isolated-staff-link-tests-secret-more-than-32-characters" };
            var builder=WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddApplication(); builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUserService,CurrentUserService>();
            builder.Services.AddScoped<AuditableEntitySaveChangesInterceptor>();
            builder.Services.AddDbContext<ApplicationDbContext>((sp,o)=>o.UseNpgsql(connection).AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>()));
            builder.Services.AddScoped<IApplicationDbContext>(sp=>sp.GetRequiredService<ApplicationDbContext>());
            builder.Services.AddIdentityCore<ApplicationUser>(o=>o.User.RequireUniqueEmail=true).AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddScoped<IJwtSessionValidator,JwtSessionValidator>(); builder.Services.AddScoped<ICurrentStaffService,CurrentStaffService>();
            builder.Services.AddScoped<IStaffAccountService,StaffAccountService>(); builder.Services.AddAccountJwtAuthentication(settings);
            builder.Services.AddAuthorization(o=>o.AddPolicy(Policies.CanManageStaff,p=>p.RequireRole(Roles.Admin)));
            var app=builder.Build(); app.UseMiddleware<GlobalExceptionHandlingMiddleware>(); app.UseAuthentication(); app.UseAuthorization(); app.MapStaffEndpoints();
            // Test harness only: production adds no resolver or Doctor schedule endpoints.
            app.MapGet("/test/session",()=>Microsoft.AspNetCore.Http.Results.NoContent()).RequireAuthorization();
            app.MapGet("/test/current-staff",async(ICurrentStaffService s)=>await s.GetCurrentStaffAsync()).RequireAuthorization();
            app.MapGet("/test/current-doctor",async(ICurrentStaffService s)=>await s.GetCurrentDoctorAsync()).RequireAuthorization();
            await app.StartAsync(); var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app,new HttpClient { BaseAddress=new Uri(address) },settings,admin);
        }
        public Task<HttpResponseMessage> Link(Staff staff,ApplicationUser user,StaffKind kind=StaffKind.Doctor)
            =>Request(Admin,HttpMethod.Post,$"/api/v1/staff/{staff.Id.Value}/account-link",new { userId=user.Id,staffKind=(int)kind });
        public Task<HttpResponseMessage> Unlink(Staff staff)=>Request(Admin,HttpMethod.Delete,$"/api/v1/staff/{staff.Id.Value}/account-link");
        public async Task<HttpResponseMessage> Request(ApplicationUser user,HttpMethod method,string path,object? body=null,string[]? roles=null,bool authenticated=true)
        {
            using var request=new HttpRequestMessage(method,path); if(body is not null) request.Content=JsonContent.Create(body);
            if(authenticated) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",new JwtTokenGenerator(Options.Create(settings))
                .GenerateToken(user.Id.ToString(),user.Email!,user.FirstName,user.LastName,roles??[Roles.Admin],user.SecurityStamp!));
            return await client.SendAsync(request);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
