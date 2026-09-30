namespace HealthERP.Application.Pharmacy.Models;

public record DrugDto(
    Guid Id,
    string Name,
    string GenericName,
    string Category,
    int StockQuantity,
    int ReorderLevel,
    DateTime ExpiryDateUtc,
    decimal UnitPrice,
    bool IsLowStock,
    bool IsExpiringSoon
);

public record PrescriptionDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid? DrugId,
    string? DrugName,
    string DoctorName,
    string MedicationName,
    string Dosage,
    string Frequency,
    int DurationDays,
    int Refills,
    string? Instructions,
    string Status,
    DateTime PrescribedDateUtc,
    DateTime? DispensedDateUtc
);
