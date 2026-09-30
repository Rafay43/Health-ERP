namespace HealthERP.Application.Reports.Models;

public record ExecutiveDashboardMetricsDto(
    decimal TotalRevenue,
    decimal TotalCollected,
    decimal TotalOutstandingBalance,
    int TotalPatients,
    int TotalAppointments,
    int TotalPrescriptionsIssued,
    int TotalLabTestsExecuted,
    List<MonthlyRevenueItemDto> MonthlyRevenueTrend,
    List<AppointmentStatusCountDto> AppointmentStatusBreakdown,
    List<TopItemCountDto> TopPrescribedDrugs,
    List<TopItemCountDto> TopLabTests
);

public record MonthlyRevenueItemDto(
    string MonthName,
    decimal TotalBilled,
    decimal TotalCollected
);

public record AppointmentStatusCountDto(
    string Status,
    int Count
);

public record TopItemCountDto(
    string Name,
    int Count
);
