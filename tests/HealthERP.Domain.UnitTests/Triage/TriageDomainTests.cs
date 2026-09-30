using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Triage;
using Xunit;

namespace HealthERP.Domain.UnitTests.Triage;

public class TriageDomainTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateTriageRecord()
    {
        var patientId = PatientId.New();

        var record = TriageRecord.Create(
            patientId,
            "Acute abdominal pain",
            "120/80",
            82,
            98,
            37.0m,
            16,
            TriageSeverity.Level3_Urgent,
            "Nurse Clara Barton");

        Assert.NotNull(record);
        Assert.Equal(patientId, record.PatientId);
        Assert.Equal(TriageSeverity.Level3_Urgent, record.Severity);
        Assert.Equal(TriageStatus.Triaged, record.Status);
        Assert.Equal("Nurse Clara Barton", record.TriageNurseName);
    }

    [Fact]
    public void Create_WithBayAssigned_ShouldSetInBayCareStatus()
    {
        var patientId = PatientId.New();

        var record = TriageRecord.Create(
            patientId,
            "Chest pain",
            "145/90",
            105,
            95,
            37.2m,
            20,
            TriageSeverity.Level1_Resuscitation,
            "Nurse Clara Barton",
            "Trauma Bay 01");

        Assert.Equal(TriageStatus.InBayCare, record.Status);
        Assert.Equal("Trauma Bay 01", record.AssignedBay);
    }

    [Fact]
    public void AssignBay_WhenActive_ShouldUpdateBayAndPhysician()
    {
        var patientId = PatientId.New();
        var record = TriageRecord.Create(
            patientId,
            "Fever and chills",
            "118/75",
            88,
            97,
            38.5m,
            18,
            TriageSeverity.Level4_LessUrgent,
            "Nurse Joy");

        record.AssignBay("ER Bay 04", "Dr. House");

        Assert.Equal("ER Bay 04", record.AssignedBay);
        Assert.Equal("Dr. House", record.AttendingPhysicianName);
        Assert.Equal(TriageStatus.InBayCare, record.Status);
    }

    [Fact]
    public void TransferToInpatient_ValidBed_ShouldSetTransferredStatus()
    {
        var patientId = PatientId.New();
        var bedId = BedId.New();

        var record = TriageRecord.Create(
            patientId,
            "Subdural hematoma",
            "160/100",
            115,
            93,
            37.1m,
            22,
            TriageSeverity.Level2_Emergent,
            "Nurse Joy");

        record.TransferToInpatient(bedId);

        Assert.Equal(TriageStatus.TransferredToInpatient, record.Status);
        Assert.Equal(bedId, record.TransferredBedId);
        Assert.NotNull(record.DispositionDateUtc);
    }

    [Fact]
    public void DischargeFromED_ActiveCase_ShouldSetDischargedStatus()
    {
        var patientId = PatientId.New();
        var record = TriageRecord.Create(
            patientId,
            "Minor laceration",
            "120/80",
            72,
            99,
            36.8m,
            14,
            TriageSeverity.Level5_NonUrgent,
            "Nurse Joy");

        record.DischargeFromED();

        Assert.Equal(TriageStatus.DischargedFromED, record.Status);
        Assert.NotNull(record.DispositionDateUtc);
    }
}
