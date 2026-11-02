using System.Security.Claims;
using HealthERP.Application.StaffMembers;
using HealthERP.Domain.StaffMembers;
using HealthERP.Infrastructure.Identity;
using HealthERP.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Staff = HealthERP.Domain.StaffMembers.StaffMembers;

namespace HealthERP.Api.IntegrationTests;

[Collection("Pharmacy PostgreSQL")]
public class CurrentStaffTests(PharmacyPostgresFixture database)
{
    private static Staff NewStaff() => Staff.Create("Test", "Staff", "same@example.invalid", "", "Doctor-looking specialty", "", "Outpatient");
    private async Task<(ApplicationUser User, Staff Staff)> Seed(string[]? roles = null, bool linked = true,
        StaffKind kind = StaffKind.Doctor, bool inactive = false, bool deleted = false)
    {
        await using var db = database.Context();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString(), Email = "same@example.invalid",
            FirstName = "Test", LastName = "Staff", SecurityStamp = "staff-tests-stamp", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        db.Users.Add(user);
        foreach (var name in roles ?? ["Doctor"])
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.NormalizedName == name.ToUpperInvariant());
            if (role is null) { role = new ApplicationRole(name) { Id = Guid.NewGuid(), NormalizedName = name.ToUpperInvariant() }; db.Roles.Add(role); }
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
        }
        var staff = NewStaff(); staff.AssignStaffKind(kind); if (linked) staff.LinkUserId(user.Id);
        if (inactive) staff.Deactivate(); db.Staff.Add(staff); if (deleted) db.Entry(staff).Property(s => s.IsDeleted).CurrentValue = true;
        await db.SaveChangesAsync(); return (user, staff);
    }
    private static IHttpContextAccessor Principal(Guid userId, bool authenticated = true)
        => new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Email, "same@example.invalid"),
             new Claim(ClaimTypes.Role, "Doctor")], authenticated ? "TestAuthentication" : null)) } };
    private static CurrentStaffService Service(HealthERP.Infrastructure.Persistence.ApplicationDbContext db, IHttpContextAccessor accessor)
        => new(db, new CurrentUserService(accessor), accessor, NullLogger<CurrentStaffService>.Instance);

    [Fact] public async Task ValidActiveLinkedStaffResolvesItsAuthoritativeId()
    {
        var (user,staff) = await Seed(kind: StaffKind.Unspecified); await using var db = database.Context();
        var result = await Service(db,Principal(user.Id)).GetCurrentStaffAsync();
        Assert.Equal(staff.Id,result.StaffId); Assert.Equal(user.Id,result.UserId); Assert.Equal(StaffKind.Unspecified,result.StaffKind);
    }
    [Theory] [InlineData(false,false,false)] [InlineData(true,true,false)] [InlineData(true,false,true)]
    public async Task MissingInactiveOrDeletedStaffFailsClosed(bool linked,bool inactive,bool deleted)
    {
        var (user,_) = await Seed(linked:linked,inactive:inactive,deleted:deleted); await using var db = database.Context();
        var service = Service(db,Principal(user.Id));
        await Assert.ThrowsAsync<StaffAccessDeniedException>(() => service.GetCurrentStaffAsync());
        await Assert.ThrowsAsync<StaffAccessDeniedException>(() => service.GetCurrentDoctorAsync());
    }
    [Theory] [InlineData("Doctor",StaffKind.Doctor,true)] [InlineData("Doctor",StaffKind.Unspecified,false)]
    [InlineData("Nurse",StaffKind.Doctor,false)] [InlineData("Admin",StaffKind.Doctor,false)]
    public async Task DoctorRequiresBothCurrentIdentityRoleAndExplicitClassification(string role,StaffKind kind,bool succeeds)
    {
        var (user,staff) = await Seed([role],kind:kind); await using var db = database.Context(); var service = Service(db,Principal(user.Id));
        // Principal deliberately claims Doctor even for Nurse/Admin: live Identity membership must win.
        if (succeeds) Assert.Equal(staff.Id,(await service.GetCurrentDoctorAsync()).StaffId);
        else await Assert.ThrowsAsync<StaffAccessDeniedException>(() => service.GetCurrentDoctorAsync());
    }
    [Fact] public async Task AdminPlusDoctorStillResolvesOnlyItsOwnLinkedDoctorStaff()
    {
        var (user,staff) = await Seed(["Admin","Doctor"]); await Seed(); await using var db = database.Context();
        Assert.Equal(staff.Id,(await Service(db,Principal(user.Id)).GetCurrentDoctorAsync()).StaffId);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task InactiveOrLockedIdentityCannotResolve(bool locked)
    {
        var (user,_) = await Seed(); await using (var change = database.Context())
        {
            var u = await change.Users.SingleAsync(x => x.Id == user.Id);
            if (locked) { u.LockoutEnabled = true; u.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10); } else u.IsActive = false;
            await change.SaveChangesAsync();
        }
        await using var db = database.Context(); await Assert.ThrowsAsync<StaffAccessDeniedException>(() => Service(db,Principal(user.Id)).GetCurrentDoctorAsync());
    }
    [Fact] public async Task AnonymousAndMalformedIdentityCannotResolve()
    {
        var (user,_) = await Seed(); await using var db = database.Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db,Principal(user.Id,false)).GetCurrentStaffAsync());
        var accessor = Principal(user.Id); accessor.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"invalid")],"Test"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db,accessor).GetCurrentStaffAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db,Principal(Guid.Empty)).GetCurrentStaffAsync());
    }
    [Fact] public async Task NoEmailNameOrFirstStaffFallback()
    {
        var (user,_) = await Seed(linked:false); await Seed(); await using var db = database.Context();
        // Every seeded account/Staff shares names and emails. None of that establishes ownership.
        await Assert.ThrowsAsync<StaffAccessDeniedException>(() => Service(db,Principal(user.Id)).GetCurrentDoctorAsync());
    }
    [Fact] public async Task MissingAccountCannotResolveEvenWithAuthenticatedPrincipal()
    { await using var db = database.Context(); await Assert.ThrowsAsync<StaffAccessDeniedException>(() => Service(db,Principal(Guid.NewGuid())).GetCurrentStaffAsync()); }
    [Fact] public async Task AccountAndStaffEmailNameChangesDoNotAlterResolution()
    {
        var (user,staff)=await Seed();
        await using(var change=database.Context()) {
            var account=await change.Users.SingleAsync(u=>u.Id==user.Id); account.Email="changed-account@example.invalid"; account.FirstName="Different account name";
            var record=await change.Staff.SingleAsync(s=>s.Id==staff.Id); record.UpdateProfile("Different","Profile","123","Surgery","Ward");
            change.Entry(record).Property(s=>s.Email).CurrentValue="changed-staff@example.invalid"; await change.SaveChangesAsync();
        }
        await using var db=database.Context(); var result=await Service(db,Principal(user.Id)).GetCurrentDoctorAsync();
        Assert.Equal(staff.Id,result.StaffId); Assert.Equal(user.Id,result.UserId);
    }
    [Fact] public async Task InvalidPersistedClassificationFailsClosed()
    {
        var (user,staff)=await Seed();
        await using(var change=database.Context()) {
            var record=await change.Staff.SingleAsync(s=>s.Id==staff.Id); change.Entry(record).Property(s=>s.StaffKind).CurrentValue=(StaffKind)42; await change.SaveChangesAsync();
        }
        await using var db=database.Context(); var service=Service(db,Principal(user.Id));
        await Assert.ThrowsAsync<StaffAccessDeniedException>(()=>service.GetCurrentStaffAsync());
        await Assert.ThrowsAsync<StaffAccessDeniedException>(()=>service.GetCurrentDoctorAsync());
    }
    [Fact] public async Task LiveRoleRemovalAndStaffDeactivationAreObservedInReusedScope()
    {
        var (user,staff) = await Seed(); await using var db = database.Context(); var service = Service(db,Principal(user.Id));
        await service.GetCurrentDoctorAsync();
        await using (var change = database.Context()) { var membership=await change.UserRoles.SingleAsync(r=>r.UserId==user.Id); change.UserRoles.Remove(membership); await change.SaveChangesAsync(); }
        await Assert.ThrowsAsync<StaffAccessDeniedException>(() => service.GetCurrentDoctorAsync());
        await using (var change = database.Context()) { (await change.Staff.SingleAsync(s=>s.Id==staff.Id)).Deactivate(); await change.SaveChangesAsync(); }
        await Assert.ThrowsAsync<StaffAccessDeniedException>(() => service.GetCurrentStaffAsync());
    }
    [Fact] public async Task ForeignKeyRejectsNonexistentAccount()
    {
        await using var db = database.Context(); var staff = NewStaff(); staff.LinkUserId(Guid.NewGuid()); db.Staff.Add(staff);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal("23503",Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    [Fact] public async Task OneAccountCannotLinkTwoStaffRows()
    {
        var (user,_) = await Seed(); await using var db = database.Context(); var second=NewStaff(); second.LinkUserId(user.Id); db.Staff.Add(second);
        var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync()); Assert.Equal("23505",Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    [Fact] public async Task DifferentAccountsCanLinkDifferentStaffRows()
    { var (a,sa)=await Seed(); var (b,sb)=await Seed(); Assert.NotEqual(a.Id,b.Id); await using var db=database.Context(); Assert.Equal(a.Id,(await db.Staff.SingleAsync(s=>s.Id==sa.Id)).UserId); Assert.Equal(b.Id,(await db.Staff.SingleAsync(s=>s.Id==sb.Id)).UserId); }
    [Fact] public async Task SoftDeletedStaffStillReservesItsAccount()
    {
        var (user,_) = await Seed(deleted:true); await using var db=database.Context(); var second=NewStaff(); second.LinkUserId(user.Id); db.Staff.Add(second);
        var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync()); Assert.Equal("23505",Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    [Fact] public async Task LinkedAccountHardDeletionIsRestricted()
    {
        var (user,_) = await Seed(); await using var db=database.Context(); db.Users.Remove(await db.Users.SingleAsync(u=>u.Id==user.Id));
        var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync()); Assert.Equal("23503",Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    [Fact] public async Task UnlinkedStaffCanCoexistAndDefaultToUnspecified()
    {
        await using var db=database.Context(); var a=NewStaff(); var b=NewStaff(); db.Staff.AddRange(a,b); await db.SaveChangesAsync();
        await using var fresh=database.Context(); var rows=await fresh.Staff.Where(s=>s.Id==a.Id||s.Id==b.Id).ToArrayAsync();
        Assert.Equal(2,rows.Length); Assert.All(rows,s=>{ Assert.Null(s.UserId); Assert.Equal(StaffKind.Unspecified,s.StaffKind); });
    }
    [Fact] public async Task ConcurrentIndependentAssociationsCannotDuplicateUserId()
    {
        var (user,_) = await Seed(linked:false); var barrier = new TwoWriterBarrier();
        async Task<bool> Link() {
            await using var db=database.Context(barrier); var staff=NewStaff(); staff.LinkUserId(user.Id); db.Staff.Add(staff);
            try { await db.SaveChangesAsync(); return true; }
            catch(DbUpdateException e) when(e.InnerException is PostgresException { SqlState:"23505" }) { return false; }
        }
        var results=await Task.WhenAll(Link(),Link()); Assert.Single(results,r=>r);
        await using var fresh=database.Context(); Assert.Single(await fresh.Staff.IgnoreQueryFilters().Where(s=>s.UserId==user.Id).ToArrayAsync());
    }
    [Fact] public void ModelMatchesMigration()
    { using var db=database.Context(); Assert.False(db.Database.HasPendingModelChanges()); }
    [Fact] public async Task AmbiguousAssociationIncludingHiddenDeletedStaffFailsClosed()
    {
        var (user,_) = await Seed(deleted:true); await using var db=database.Context();
        await using var tx=await db.Database.BeginTransactionAsync();
        try {
            // Fault injection only in the guarded disposable test database. PostgreSQL transactional
            // DDL restores the unique index and removes the conflicting row on rollback.
            await db.Database.ExecuteSqlRawAsync("DROP INDEX staff.\"IX_staff_UserId\"");
            var duplicate=NewStaff(); duplicate.LinkUserId(user.Id); duplicate.AssignStaffKind(StaffKind.Doctor);
            db.Staff.Add(duplicate); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<StaffAccessDeniedException>(() => Service(db,Principal(user.Id)).GetCurrentStaffAsync());
            await Assert.ThrowsAsync<StaffAccessDeniedException>(() => Service(db,Principal(user.Id)).GetCurrentDoctorAsync());
        }
        finally { await tx.RollbackAsync(); }
    }
    [Fact] public async Task MigrationUpPreservesLegacyRowsAsUnlinkedAndUnspecified()
    {
        var (user,staff)=await Seed(); await using var db=database.Context();
        var migrations=db.GetService<IMigrationsAssembly>();
        var entry=Assert.Single(migrations.Migrations,p=>p.Key.EndsWith("_AddVerifiedStaffIdentityAssociation"));
        var migration=migrations.CreateMigration(entry.Value,db.Database.ProviderName!);
        var generator=db.GetService<IMigrationsSqlGenerator>();
        await using var tx=await db.Database.BeginTransactionAsync();
        try {
            // Exercise the generated Down/Up schema operations against pre-existing data, with
            // rollback restoring the shared test database. No operational database is touched.
            foreach(var command in generator.Generate(migration.DownOperations,db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            foreach(var command in generator.Generate(migration.UpOperations,db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            var legacy=await db.Staff.AsNoTracking().SingleAsync(s=>s.Id==staff.Id);
            Assert.Null(legacy.UserId); Assert.Equal(StaffKind.Unspecified,legacy.StaffKind);
            Assert.Equal(staff.Email,legacy.Email); Assert.Equal(staff.FirstName,legacy.FirstName);
            Assert.True(await db.UserRoles.AnyAsync(r=>r.UserId==user.Id));
        }
        finally { await tx.RollbackAsync(); }
    }
    private sealed class TwoWriterBarrier : SaveChangesInterceptor
    {
        private int arrivals; private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        { if(Interlocked.Increment(ref arrivals)==2) ready.SetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(20),ct); return result; }
    }
}
