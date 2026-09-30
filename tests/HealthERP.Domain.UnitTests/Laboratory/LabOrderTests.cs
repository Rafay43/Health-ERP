using HealthERP.Domain.Patients;
using HealthERP.Domain.Laboratory;
using Xunit;

namespace HealthERP.Domain.UnitTests.Laboratory;

public class LabOrderTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateOrderedStatus()
    {
        // Arrange
        var patientId = PatientId.New();

        // Act
        var order = LabOrder.Create(
            patientId,
            null,
            "Dr. Smith",
            "CBC Test",
            "CBC-01",
            "STAT");

        // Assert
        Assert.NotNull(order);
        Assert.Equal(LabOrderStatus.Ordered, order.Status);
        Assert.Equal("STAT", order.Priority);
        Assert.False(order.IsAbnormal);
    }

    [Fact]
    public void RecordResult_ValidInput_ShouldMarkCompletedAndSetAbnormalFlag()
    {
        // Arrange
        var order = LabOrder.Create(
            PatientId.New(),
            null,
            "Dr. Smith",
            "CBC Test",
            "CBC-01",
            "Routine");

        // Act
        order.RecordResult("18.5", "High WBC", "Lab Tech John", isAbnormal: true);

        // Assert
        Assert.Equal(LabOrderStatus.Completed, order.Status);
        Assert.Equal("18.5", order.ResultValue);
        Assert.True(order.IsAbnormal);
        Assert.NotNull(order.CompletedDateUtc);
    }
}
