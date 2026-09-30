using HealthERP.Domain.Laboratory;
using Xunit;

namespace HealthERP.Domain.UnitTests.Laboratory;

public class LabTestTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateLabTest()
    {
        // Act
        var test = LabTest.Create(
            "Lipid Profile Panel",
            "LIP-01",
            "Biochemistry",
            "< 200 mg/dL",
            "mg/dL",
            55.00m,
            "Serum");

        // Assert
        Assert.NotNull(test);
        Assert.Equal("Lipid Profile Panel", test.Name);
        Assert.Equal("LIP-01", test.Code);
        Assert.True(test.IsActive);
    }

    [Fact]
    public void Create_EmptyCode_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            LabTest.Create("Test", "", "Cat", "1-10", "mg", 10m, "Blood"));
    }
}
