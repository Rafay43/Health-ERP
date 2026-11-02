using HealthERP.Domain.Inpatient;

namespace HealthERP.Domain.UnitTests.Inpatient;

public class BedAvailabilityTests
{
    [Fact]
    public void MaintenanceBed_CannotBeOccupied()
    {
        var bed = Bed.Create("Test", "1", "1", "General", 100); bed.MarkMaintenance();
        Assert.Throws<InvalidOperationException>(() => bed.Occupy()); Assert.Equal(BedStatus.Maintenance, bed.Status);
    }
}
