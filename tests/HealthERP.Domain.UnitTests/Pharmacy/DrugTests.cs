using HealthERP.Domain.Pharmacy;
using Xunit;

namespace HealthERP.Domain.UnitTests.Pharmacy;

public class DrugTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateDrug()
    {
        // Act
        var drug = Drug.Create(
            "Amoxicillin 500mg",
            "Amoxicillin",
            "Antibiotics",
            100,
            20,
            DateTime.UtcNow.AddMonths(12),
            15.50m);

        // Assert
        Assert.NotNull(drug);
        Assert.NotEqual(Guid.Empty, drug.Id.Value);
        Assert.Equal("Amoxicillin 500mg", drug.Name);
        Assert.Equal(100, drug.StockQuantity);
        Assert.False(drug.IsLowStock);
        Assert.False(drug.IsExpiringSoon);
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Drug.Create("", "Generic", "Category", 10, 5, DateTime.UtcNow.AddYears(1), 10m));
    }

    [Fact]
    public void DeductStock_ValidQuantity_ShouldReduceStock()
    {
        // Arrange
        var drug = Drug.Create("Test Drug", "Generic", "General", 50, 10, DateTime.UtcNow.AddMonths(6), 5m);

        // Act
        drug.DeductStock(20);

        // Assert
        Assert.Equal(30, drug.StockQuantity);
    }

    [Fact]
    public void DeductStock_ExcessQuantity_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var drug = Drug.Create("Test Drug", "Generic", "General", 10, 5, DateTime.UtcNow.AddMonths(6), 5m);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => drug.DeductStock(15));
    }

    [Fact]
    public void IsLowStock_StockAtOrBelowReorderLevel_ShouldReturnTrue()
    {
        // Arrange
        var drug = Drug.Create("Test Drug", "Generic", "General", 10, 10, DateTime.UtcNow.AddMonths(6), 5m);

        // Assert
        Assert.True(drug.IsLowStock);
    }
}
