namespace HealthERP.Application.Dashboard.Models;

public record DashboardKpiDto(
    int TotalPatientsCount,
    int TodayAppointmentsCount,
    int TodayCompletedAppointmentsCount,
    int TodayPendingAppointmentsCount,
    int TotalBedsCount,
    int OccupiedBedsCount,
    double OccupancyPercentage,
    decimal MonthlyRevenue,
    decimal TotalBalanceDue,
    int ActiveEmergencyQueueCount,
    int CriticalEmergencyCount
);

public record RevenueMonthlyTrendDto(
    string Month,
    decimal Revenue,
    decimal Balance
);

public record WardBedDistributionDto(
    string WardName,
    int TotalBeds,
    int OccupiedBeds
);

public record EsiDistributionDto(
    string SeverityName,
    int SeverityLevel,
    int Count
);

public record DashboardAppointmentDto(
    Guid Id,
    string PatientName,
    string DoctorName,
    DateTime StartDateTimeUtc,
    string Reason,
    string Status
);

public record DashboardTriageDto(
    Guid Id,
    string PatientName,
    int SeverityLevel,
    string SeverityName,
    string ChiefComplaint,
    string? AssignedBay,
    DateTime ArrivalTimeUtc
);

public record DashboardDrugAlertDto(
    Guid Id,
    string Name,
    int StockQuantity,
    int ReorderLevel
);

public record DashboardMetricsDto(
    DashboardKpiDto Kpis,
    List<RevenueMonthlyTrendDto> RevenueTrend,
    List<WardBedDistributionDto> WardBedDistribution,
    List<EsiDistributionDto> EsiDistribution,
    List<DashboardAppointmentDto> TodayAppointments,
    List<DashboardTriageDto> ActiveEmergencyQueue,
    List<DashboardDrugAlertDto> LowStockAlerts
);
