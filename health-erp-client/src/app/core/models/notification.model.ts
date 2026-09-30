export interface AppNotification {
  id: string;
  Id?: string;
  targetUserId?: string;
  targetRole: string;
  title: string;
  message: string;
  linkUrl?: string;
  typeName: string; // EmergencyAlert, LabAbnormalFlag, LowStockWarning, InpatientBedDischarge, AppointmentReminder, SystemInfo
  typeValue: number;
  severityName: string; // Info, Warning, Critical
  severityValue: number;
  isRead: boolean;
  IsRead?: boolean;
  readAtUtc?: string;
  createdAtUtc: string;
}

export interface SendNotificationRequest {
  title: string;
  message: string;
  type: number;
  severity: number;
  linkUrl?: string;
  targetUserId?: string;
  targetRole?: string;
}
