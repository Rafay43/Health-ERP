using HealthERP.SharedKernel;

namespace HealthERP.Domain.Pharmacy;

public class Drug : BaseEntity<DrugId>, IAuditable
{
    public string Name { get; private set; } = default!;
    public string GenericName { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public int StockQuantity { get; private set; }
    public int ReorderLevel { get; private set; }
    public DateTime ExpiryDateUtc { get; private set; }
    public decimal UnitPrice { get; private set; }

    public bool IsLowStock => StockQuantity <= ReorderLevel;
    public bool IsExpiringSoon => ExpiryDateUtc <= DateTime.UtcNow.AddDays(30);

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private Drug() { } // EF Core

    private Drug(
        DrugId id,
        string name,
        string genericName,
        string category,
        int stockQuantity,
        int reorderLevel,
        DateTime expiryDateUtc,
        decimal unitPrice)
    {
        Id = id;
        Name = name;
        GenericName = genericName;
        Category = category;
        StockQuantity = stockQuantity;
        ReorderLevel = reorderLevel;
        ExpiryDateUtc = expiryDateUtc;
        UnitPrice = unitPrice;
    }

    public static Drug Create(
        string name,
        string genericName,
        string category,
        int stockQuantity,
        int reorderLevel,
        DateTime expiryDateUtc,
        decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Drug name is required.", nameof(name));
        if (stockQuantity < 0)
            throw new ArgumentException("Stock quantity cannot be negative.", nameof(stockQuantity));
        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

        return new Drug(
            DrugId.New(),
            name.Trim(),
            genericName?.Trim() ?? string.Empty,
            category?.Trim() ?? "General",
            stockQuantity,
            reorderLevel,
            expiryDateUtc,
            unitPrice);
    }

    public void UpdateDetails(
        string name,
        string genericName,
        string category,
        int reorderLevel,
        DateTime expiryDateUtc,
        decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Drug name is required.", nameof(name));

        Name = name.Trim();
        GenericName = genericName?.Trim() ?? string.Empty;
        Category = category?.Trim() ?? "General";
        ReorderLevel = reorderLevel;
        ExpiryDateUtc = expiryDateUtc;
        UnitPrice = unitPrice;
    }

    public void UpdateStock(int newQuantity)
    {
        if (newQuantity < 0)
            throw new InvalidOperationException("Stock quantity cannot be negative.");
        StockQuantity = newQuantity;
    }

    public void DeductStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity to deduct must be positive.", nameof(quantity));
        if (StockQuantity < quantity)
            throw new InvalidOperationException($"Insufficient stock for {Name}. Required: {quantity}, Available: {StockQuantity}");
        
        StockQuantity -= quantity;
    }

    public void AddStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity to add must be positive.", nameof(quantity));
        StockQuantity += quantity;
    }
}
