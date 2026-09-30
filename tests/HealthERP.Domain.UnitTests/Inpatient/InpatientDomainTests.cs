using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Patients;
using Xunit;

namespace HealthERP.Domain.UnitTests.Inpatient;

public class InpatientDomainTests
{
    [Fact]
    public void Bed_Create_ShouldInitializeAvailable()
    {
        var bed = Bed.Create("ICU Ward", "ICU-101", "Bed-A", "ICU Bed", 350.00m);

        Assert.NotNull(bed);
        Assert.Equal("ICU Ward", bed.WardName);
        Assert.Equal("ICU-101", bed.RoomNumber);
        Assert.Equal("Bed-A", bed.BedNumber);
        Assert.Equal(BedStatus.Available, bed.Status);
    }

    [Fact]
    public void Bed_Occupy_WhenAvailable_ShouldChangeStatusToOccupied()
    {
        var bed = Bed.Create("ICU Ward", "ICU-101", "Bed-A", "ICU Bed", 350.00m);
        bed.Occupy();

        Assert.Equal(BedStatus.Occupied, bed.Status);
    }

    [Fact]
    public void Bed_Occupy_WhenAlreadyOccupied_ShouldThrowInvalidOperationException()
    {
        var bed = Bed.Create("ICU Ward", "ICU-101", "Bed-A", "ICU Bed", 350.00m);
        bed.Occupy();

        Assert.Throws<InvalidOperationException>(() => bed.Occupy());
    }

    [Fact]
    public void Bed_Release_WhenOccupied_ShouldChangeStatusToAvailable()
    {
        var bed = Bed.Create("ICU Ward", "ICU-101", "Bed-A", "ICU Bed", 350.00m);
        bed.Occupy();
        bed.Release();

        Assert.Equal(BedStatus.Available, bed.Status);
    }

    [Fact]
    public void Admission_Create_ShouldInitializeActive()
    {
        var patientId = PatientId.New();
        var bedId = BedId.New();

        var admission = Admission.Create(patientId, bedId, "Dr. Sarah Connor", "Acute Appendicitis", 150.00m);

        Assert.NotNull(admission);
        Assert.Equal(patientId, admission.PatientId);
        Assert.Equal(bedId, admission.BedId);
        Assert.False(admission.IsDischarged);
        Assert.Equal(0, admission.TotalRoomCharge);
    }

    [Fact]
    public void Admission_Discharge_ShouldCalculateStayAndCharges()
    {
        var patientId = PatientId.New();
        var bedId = BedId.New();

        var admission = Admission.Create(patientId, bedId, "Dr. Sarah Connor", "Acute Appendicitis", 200.00m);
        admission.Discharge("Recovered post-appendectomy.");

        Assert.True(admission.IsDischarged);
        Assert.NotNull(admission.DischargeDateUtc);
        Assert.Equal("Recovered post-appendectomy.", admission.DischargeSummary);
        Assert.True(admission.TotalRoomCharge >= 200.00m); // Minimum 1 day calculation
    }

    [Fact]
    public void Admission_Discharge_WhenAlreadyDischarged_ShouldThrowInvalidOperationException()
    {
        var patientId = PatientId.New();
        var bedId = BedId.New();

        var admission = Admission.Create(patientId, bedId, "Dr. Sarah Connor", "Acute Appendicitis", 200.00m);
        admission.Discharge("Discharged");

        Assert.Throws<InvalidOperationException>(() => admission.Discharge("Again"));
    }
}
