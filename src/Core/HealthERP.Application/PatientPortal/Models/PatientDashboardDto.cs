namespace HealthERP.Application.PatientPortal.Models;

public record PatientDashboardDto(
    PatientPortalProfileDto Profile,
    PatientAppointmentDto? NextAppointment,
    int ActivePrescriptionCount,
    int RecentLabResultCount,
    decimal OutstandingBalance,
    LatestVitalsSummaryDto? LatestVitals);
