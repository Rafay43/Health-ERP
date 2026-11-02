using HealthERP.SharedKernel;

namespace HealthERP.Domain.Inpatient;

public class Bed : BaseEntity<BedId>, IAuditable
{
    public string WardName { get; private set; } = default!;
    public string RoomNumber { get; private set; } = default!;
    public string BedNumber { get; private set; } = default!;
    public string BedType { get; private set; } = default!; // General, Private, ICU, Pediatric
    public decimal DailyRate { get; private set; }
    public BedStatus Status { get; private set; } = BedStatus.Available;

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private Bed() { } // EF Core

    private Bed(
        BedId id,
        string wardName,
        string roomNumber,
        string bedNumber,
        string bedType,
        decimal dailyRate)
    {
        Id = id;
        WardName = wardName;
        RoomNumber = roomNumber;
        BedNumber = bedNumber;
        BedType = bedType;
        DailyRate = dailyRate;
        Status = BedStatus.Available;
    }

    public static Bed Create(
        string wardName,
        string roomNumber,
        string bedNumber,
        string bedType,
        decimal dailyRate)
    {
        if (string.IsNullOrWhiteSpace(wardName))
            throw new ArgumentException("Ward name is required.", nameof(wardName));
        if (string.IsNullOrWhiteSpace(roomNumber))
            throw new ArgumentException("Room number is required.", nameof(roomNumber));
        if (string.IsNullOrWhiteSpace(bedNumber))
            throw new ArgumentException("Bed number is required.", nameof(bedNumber));
        if (dailyRate < 0)
            throw new ArgumentException("Daily rate cannot be negative.", nameof(dailyRate));

        return new Bed(
            BedId.New(),
            wardName.Trim(),
            roomNumber.Trim(),
            bedNumber.Trim(),
            bedType?.Trim() ?? "General",
            dailyRate);
    }

    public void Occupy()
    {
        if (Status != BedStatus.Available)
            throw new InvalidOperationException("Only an available bed can be occupied.");
        Status = BedStatus.Occupied;
    }

    public void Release()
    {
        Status = BedStatus.Available;
    }

    public void MarkMaintenance()
    {
        Status = BedStatus.Maintenance;
    }
}
