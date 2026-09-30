namespace HealthERP.Domain.Notifications;

public enum NotificationType
{
    EmergencyAlert = 1,
    LabAbnormalFlag = 2,
    LowStockWarning = 3,
    InpatientBedDischarge = 4,
    AppointmentReminder = 5,
    SystemInfo = 6
}

public enum NotificationSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}
