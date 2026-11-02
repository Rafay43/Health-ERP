using System.Net.Http.Json;
using System.Text.Json;
using HealthERP.Application.Common.Security;
using HealthERP.Application.DoctorWorkspace.Commands;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace HealthERP.Api.IntegrationTests;

public partial class DoctorPatientSnapshotTests
{
    [Theory] [InlineData(AppointmentStatus.Scheduled)] [InlineData(AppointmentStatus.Confirmed)] [InlineData(AppointmentStatus.Completed)]
    public async Task RecordNote_QualifyingRelationshipPersistsAuthorClockAuditAndRefresh(AppointmentStatus status)
    {
        var doctor=await Doctor(); var patient=await Patient(); await Relationship(doctor.Staff,patient,status,days:-365);
        await using var api=await Api.Create(database.ConnectionString);
        using var response=await api.Post(doctor.User,patient.Id.Value,new { notes="  Real clinical note  ",bloodPressure="139/91",heartRateBpm=88,temperatureCelsius=38.15m,weightKg=61.25m });
        Assert.Equal(201,(int)response.StatusCode); var dto=(await response.Content.ReadFromJsonAsync<RecordedClinicalNoteDto>())!;
        Assert.Equal(Now,dto.RecordedAtUtc); using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Properties(json.RootElement,"noteId","recordedAtUtc");
        await using var db=database.Context(); var note=await db.ClinicalNotes.AsNoTracking().SingleAsync(n=>n.Id==new ClinicalNoteId(dto.NoteId));
        Assert.Equal(doctor.Staff.Id,note.RecordedByStaffId); Assert.Equal(doctor.User.Id.ToString(),note.CreatedBy); Assert.NotEqual(note.CreatedBy,note.RecordedByStaffId!.Value.Value.ToString());
        Assert.Equal("Real clinical note",note.Notes); Assert.Equal("Verified Doctor",note.DoctorName); Assert.Equal(Now,note.NoteDateUtc);
        using var refreshed=await api.Get(doctor.User,patient.Id.Value); var snapshot=await Read(refreshed);
        var item=Assert.Single(snapshot.RecentNotes); Assert.Equal(dto.NoteId,item.NoteId); Assert.Equal("Verified Doctor",item.RecordedByDisplayName);
        Assert.Equal(88,snapshot.LatestVitals!.HeartRateBpm); Assert.Equal(38.15m,snapshot.LatestVitals.TemperatureCelsius);
    }
    [Theory] [InlineData(AppointmentStatus.Cancelled)] [InlineData(AppointmentStatus.NoShow)]
    public async Task RecordNote_DisallowedStatusCannotWrite(AppointmentStatus status)
    {
        var doctor=await Doctor(); var patient=await Patient(); await Relationship(doctor.Staff,patient,status);
        await using var api=await Api.Create(database.ConnectionString); using var r=await api.Post(doctor.User,patient.Id.Value,new {notes="private"});
        Assert.Equal(404,(int)r.StatusCode); await AssertNoNotes(patient);
    }
    [Theory] [InlineData("unlinked",403)] [InlineData("unspecified",403)] [InlineData("inactive-staff",403)] [InlineData("deleted-staff",403)]
    [InlineData("inactive-account",401)] [InlineData("locked-account",401)]
    public async Task RecordNote_IneligibleDoctorCannotWrite(string state,int expected)
    {
        var doctor=await Doctor(state:state); var patient=await Patient(); await Relationship(doctor.Staff,patient);
        await using var api=await Api.Create(database.ConnectionString); using var r=await api.Post(doctor.User,patient.Id.Value,new {notes="private"});
        Assert.Equal(expected,(int)r.StatusCode); await AssertNoNotes(patient);
    }
    [Theory] [InlineData(Roles.Admin,403)] [InlineData(Roles.Nurse,403)] [InlineData(Roles.Patient,403)] [InlineData(null,401)]
    public async Task RecordNote_OnlyAuthenticatedDoctorCanWrite(string? role,int expected)
    {
        var doctor=await Doctor(role is null?[]:[role]); var patient=await Patient(); await Relationship(doctor.Staff,patient);
        await using var api=await Api.Create(database.ConnectionString); using var r=await api.Post(doctor.User,patient.Id.Value,new {notes="private"},role is null?null:[role],role is null);
        Assert.Equal(expected,(int)r.StatusCode); await AssertNoNotes(patient);
    }
    [Fact] public async Task RecordNote_LiveDoctorRoleIsRequiredDespiteClaim()
    {
        var doctor=await Doctor([Roles.Nurse]); var patient=await Patient(); await Relationship(doctor.Staff,patient);
        await using var api=await Api.Create(database.ConnectionString); using var r=await api.Post(doctor.User,patient.Id.Value,new {notes="private"},[Roles.Doctor]);
        Assert.Equal(403,(int)r.StatusCode); await AssertNoNotes(patient);
    }
    [Fact] public async Task RecordNote_AdminDoctorCannotWriteOtherDoctorsPatient()
    {
        var a=await Doctor([Roles.Admin,Roles.Doctor]); var b=await Doctor(); var x=await Patient(); var y=await Patient(); await Relationship(a.Staff,x); await Relationship(b.Staff,y);
        await using var api=await Api.Create(database.ConnectionString);
        using var yes=await api.Post(a.User,x.Id.Value,new {notes="own"},[Roles.Admin,Roles.Doctor]); Assert.Equal(201,(int)yes.StatusCode);
        using var no=await api.Post(a.User,y.Id.Value,new {notes="other"},[Roles.Admin,Roles.Doctor]); Assert.Equal(404,(int)no.StatusCode);
        using var other=await api.Post(b.User,x.Id.Value,new {notes="other"}); Assert.Equal(404,(int)other.StatusCode); await AssertNoNotes(y);
    }
    [Theory] [InlineData("missing")] [InlineData("unrelated")] [InlineData("other-doctor")] [InlineData("deleted-patient")] [InlineData("deleted-appointment")]
    public async Task RecordNote_UnavailablePatientsHaveSameSafeResponseWithoutWriting(string state)
    {
        var doctor=await Doctor(); var patient=await Patient(deleted:state=="deleted-patient");
        if(state=="other-doctor") {var other=await Doctor();await Relationship(other.Staff,patient);}
        if(state is "deleted-patient" or "deleted-appointment") await Relationship(doctor.Staff,patient,deleted:state=="deleted-appointment");
        await using var api=await Api.Create(database.ConnectionString); using var r=await api.Post(doctor.User,state=="missing"?Guid.NewGuid():patient.Id.Value,new {notes="private"});
        Assert.Equal(404,(int)r.StatusCode); using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync()); Assert.Equal("Patient was not found.",json.RootElement.GetProperty("detail").GetString()); await AssertNoNotes(patient);
    }
    [Theory] [InlineData("doctorName")] [InlineData("authorName")] [InlineData("staffId")] [InlineData("doctorId")] [InlineData("userId")]
    [InlineData("recordedByStaffId")] [InlineData("createdBy")] [InlineData("noteDateUtc")] [InlineData("patientId")]
    public async Task RecordNote_BrowserAuthorAndTimestampFieldsAreRejected(string field)
    {
        var doctor=await Doctor(); var patient=await Patient(); await Relationship(doctor.Staff,patient); var spoof=await Doctor();
        await using var api=await Api.Create(database.ConnectionString);
        using var r=await api.Post(doctor.User,patient.Id.Value,new Dictionary<string,object>{{"notes","real"},{field,spoof.Staff.Id.Value.ToString()}});
        Assert.Equal(400,(int)r.StatusCode); await AssertNoNotes(patient);
    }
    [Theory] [InlineData("empty")] [InlineData("whitespace")] [InlineData("bp-format")] [InlineData("bp-bounds")] [InlineData("bp-order")]
    [InlineData("pulse-zero")] [InlineData("pulse-high")] [InlineData("temp-low")] [InlineData("temp-high")]
    [InlineData("weight-zero")] [InlineData("weight-high")] [InlineData("precision")] [InlineData("long-notes")] [InlineData("long-diagnosis")] [InlineData("long-complaint")]
    public async Task RecordNote_InvalidContentIsValidationErrorAndDoesNotPersist(string scenario)
    {
        var body=new Dictionary<string,object?>(); if(scenario!="empty") body["notes"]="valid note";
        switch(scenario) {
            case "whitespace": body["notes"]=" \r\n ";body["diagnosis"]=" ";body["bloodPressure"]=" ";break;
            case "bp-format":body["bloodPressure"]="normal";break;case "bp-bounds":body["bloodPressure"]="999/99";break;case "bp-order":body["bloodPressure"]="80/90";break;
            case "pulse-zero":body["heartRateBpm"]=0;break;case "pulse-high":body["heartRateBpm"]=301;break;
            case "temp-low":body["temperatureCelsius"]=24;break;case "temp-high":body["temperatureCelsius"]=46;break;
            case "weight-zero":body["weightKg"]=0;break;case "weight-high":body["weightKg"]=501;break;case "precision":body["weightKg"]=61.234m;break;
            case "long-notes":body["notes"]=new string('a',2001);break;case "long-diagnosis":body["diagnosis"]=new string('a',301);break;case "long-complaint":body["chiefComplaint"]=new string('a',501);break;
        }
        var doctor=await Doctor(); var patient=await Patient(); await Relationship(doctor.Staff,patient);await using var api=await Api.Create(database.ConnectionString);
        using var r=await api.Post(doctor.User,patient.Id.Value,body); Assert.Equal(400,(int)r.StatusCode); await AssertNoNotes(patient);
    }
    [Theory] [InlineData("note")] [InlineData("vitals")] [InlineData("complaint")] [InlineData("diagnosis")]
    public async Task RecordNote_SingleMeaningfulClinicalValueIsEnough(string kind)
    {
        var doctor=await Doctor(); var patient=await Patient(); await Relationship(doctor.Staff,patient,days:365);
        var input=new Dictionary<string,object>{{kind=="note"?"notes":kind=="vitals"?"heartRateBpm":kind=="complaint"?"chiefComplaint":"diagnosis",kind=="vitals"?95:"recorded"}};
        await using var api=await Api.Create(database.ConnectionString); using var r=await api.Post(doctor.User,patient.Id.Value,input); Assert.Equal(201,(int)r.StatusCode);
        await using var db=database.Context(); var note=await db.ClinicalNotes.SingleAsync(n=>n.PatientId==patient.Id);
        Assert.Null(note.BloodPressure); Assert.Null(note.TemperatureCelsius); Assert.Null(note.WeightKg);
        using var fresh=await api.Get(doctor.User,patient.Id.Value); var dto=await Read(fresh); if(kind!="vitals") Assert.Null(dto.LatestVitals);
    }
    [Fact] public async Task RecordNote_ReauthorizesAfterSnapshotWasOpened()
    {
        var doctor=await Doctor(); var patient=await Patient(); var appointment=await Relationship(doctor.Staff,patient);
        await using var api=await Api.Create(database.ConnectionString); using var initial=await api.Get(doctor.User,patient.Id.Value); await Read(initial);
        await using(var db=database.Context()){(await db.Appointments.SingleAsync(a=>a.Id==appointment.Id)).Cancel();await db.SaveChangesAsync();}
        using var r=await api.Post(doctor.User,patient.Id.Value,new {notes="must not save"}); Assert.Equal(404,(int)r.StatusCode); await AssertNoNotes(patient);
    }
    [Fact] public async Task RecordNote_LegacyAuthorNeverBecomesVerifiedAndSoftDeletedAuthorIsUnavailable()
    {
        var doctor=await Doctor(); var patient=await Patient(); await Relationship(doctor.Staff,patient); var legacy=await Note(patient,Now.AddHours(-1));
        await using var api=await Api.Create(database.ConnectionString); using var created=await api.Post(doctor.User,patient.Id.Value,new {notes="new"}); Assert.Equal(201,(int)created.StatusCode);
        using var refreshed=await api.Get(doctor.User,patient.Id.Value); var dto=await Read(refreshed); Assert.Null(dto.RecentNotes.Single(n=>n.NoteId==legacy.Id.Value).RecordedByDisplayName);
        // Updating the referenced Staff name changes verified display through the FK, never through DoctorName.
        await using(var db=database.Context()){var staff=await db.Staff.SingleAsync(s=>s.Id==doctor.Staff.Id);db.Entry(staff).Property(s=>s.FirstName).CurrentValue="Renamed";await db.SaveChangesAsync();}
        using var renamed=await api.Get(doctor.User,patient.Id.Value); Assert.Equal("Renamed Doctor",(await Read(renamed)).RecentNotes[0].RecordedByDisplayName);
        var other=await Doctor();await Relationship(other.Staff,patient);
        await using(var db=database.Context()){var staff=await db.Staff.SingleAsync(s=>s.Id==doctor.Staff.Id);db.Entry(staff).Property(s=>s.IsDeleted).CurrentValue=true;await db.SaveChangesAsync();}
        using var hidden=await api.Get(other.User,patient.Id.Value);Assert.All((await Read(hidden)).RecentNotes,n=>Assert.Null(n.RecordedByDisplayName));
    }
    [Fact] public async Task RecordNote_ForeignKeyRejectsMissingStaffAndRestrictsDeletion()
    {
        var doctor=await Doctor();var patient=await Patient();await Relationship(doctor.Staff,patient);
        using(var model=database.Context()){var fk=Assert.Single(model.Model.FindEntityType(typeof(ClinicalNote))!.GetForeignKeys(),f=>f.Properties.Any(p=>p.Name=="RecordedByStaffId"));Assert.Equal(DeleteBehavior.Restrict,fk.DeleteBehavior);Assert.True(fk.Properties[0].IsNullable);}
        await using(var db=database.Context()){var n=ClinicalNote.RecordByDoctor(patient.Id,new(Guid.NewGuid()),"Server",Now,null,null,"note",null,null,null,null);db.ClinicalNotes.Add(n);
            var e=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());Assert.Equal("23503",Assert.IsType<PostgresException>(e.InnerException).SqlState);}
        await using(var api=await Api.Create(database.ConnectionString)){using var r=await api.Post(doctor.User,patient.Id.Value,new {notes="note"});Assert.Equal(201,(int)r.StatusCode);}
        await using(var db=database.Context()){db.Staff.Remove(await db.Staff.SingleAsync(s=>s.Id==doctor.Staff.Id));var e=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());Assert.Equal("23503",Assert.IsType<PostgresException>(e.InnerException).SqlState);}
    }
    [Fact] public async Task RecordNote_MigrationAddsNullableAuthorWithoutLegacyInference()
    {
        var doctor=await Doctor();var patient=await Patient();var note=await Note(patient,Now);
        await using var db=database.Context();var assembly=db.GetService<IMigrationsAssembly>();var entry=Assert.Single(assembly.Migrations,p=>p.Key.EndsWith("_AddClinicalNoteRecordedByStaff"));
        var migration=assembly.CreateMigration(entry.Value,db.Database.ProviderName!);var generator=db.GetService<IMigrationsSqlGenerator>();
        await using var tx=await db.Database.BeginTransactionAsync();
        try {foreach(var c in generator.Generate(migration.DownOperations,db.Model))await db.Database.ExecuteSqlRawAsync(c.CommandText);
            foreach(var c in generator.Generate(migration.UpOperations,db.Model))await db.Database.ExecuteSqlRawAsync(c.CommandText);
            var legacy=await db.ClinicalNotes.AsNoTracking().SingleAsync(n=>n.Id==note.Id);Assert.Null(legacy.RecordedByStaffId);Assert.Equal(UntrustedAuthor,legacy.DoctorName);Assert.False(db.Database.HasPendingModelChanges());}
        finally{await tx.RollbackAsync();}
    }
    private async Task AssertNoNotes(Patient patient){await using var db=database.Context();Assert.False(await db.ClinicalNotes.AnyAsync(n=>n.PatientId==patient.Id));}
}
