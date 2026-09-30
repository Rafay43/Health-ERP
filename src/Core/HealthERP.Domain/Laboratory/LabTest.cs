using HealthERP.SharedKernel;

namespace HealthERP.Domain.Laboratory;

public class LabTest : BaseEntity<LabTestId>, IAuditable
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public string ReferenceRange { get; private set; } = default!;
    public string Unit { get; private set; } = default!;
    public decimal StandardPrice { get; private set; }
    public string SampleType { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private LabTest() { } // EF Core

    private LabTest(
        LabTestId id,
        string name,
        string code,
        string category,
        string referenceRange,
        string unit,
        decimal standardPrice,
        string sampleType)
    {
        Id = id;
        Name = name;
        Code = code;
        Category = category;
        ReferenceRange = referenceRange;
        Unit = unit;
        StandardPrice = standardPrice;
        SampleType = sampleType;
        IsActive = true;
    }

    public static LabTest Create(
        string name,
        string code,
        string category,
        string referenceRange,
        string unit,
        decimal standardPrice,
        string sampleType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Lab test name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Lab test code is required.", nameof(code));
        if (standardPrice < 0)
            throw new ArgumentException("Standard price cannot be negative.", nameof(standardPrice));

        return new LabTest(
            LabTestId.New(),
            name.Trim(),
            code.Trim().ToUpper(),
            category?.Trim() ?? "General",
            referenceRange?.Trim() ?? string.Empty,
            unit?.Trim() ?? string.Empty,
            standardPrice,
            sampleType?.Trim() ?? "Blood");
    }

    public void UpdateDetails(
        string name,
        string category,
        string referenceRange,
        string unit,
        decimal standardPrice,
        string sampleType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Lab test name is required.", nameof(name));

        Name = name.Trim();
        Category = category?.Trim() ?? "General";
        ReferenceRange = referenceRange?.Trim() ?? string.Empty;
        Unit = unit?.Trim() ?? string.Empty;
        StandardPrice = standardPrice;
        SampleType = sampleType?.Trim() ?? "Blood";
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
