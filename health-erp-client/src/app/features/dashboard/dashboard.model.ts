export interface DashboardKpi {
  totalPatientsCount: number;
  todayAppointmentsCount: number;
  todayCompletedAppointmentsCount: number;
  todayPendingAppointmentsCount: number;
  totalBedsCount: number;
  occupiedBedsCount: number;
  occupancyPercentage: number;
  monthlyRevenue: number;
  totalBalanceDue: number;
  activeEmergencyQueueCount: number;
  criticalEmergencyCount: number;
}

export interface RevenueMonthlyTrend {
  month: string;
  revenue: number;
  balance: number;
}

export interface WardBedDistribution {
  wardName: string;
  totalBeds: number;
  occupiedBeds: number;
}

export interface EsiDistribution {
  severityName: string;
  severityLevel: number;
  count: number;
}

export interface DashboardAppointment {
  id: string;
  patientName: string;
  doctorName: string;
  startDateTimeUtc: string;
  reason: string;
  status: string;
}

export interface DashboardTriage {
  id: string;
  patientName: string;
  severityLevel: number;
  severityName: string;
  chiefComplaint: string;
  assignedBay?: string;
  arrivalTimeUtc: string;
}

export interface DashboardDrugAlert {
  id: string;
  name: string;
  stockQuantity: number;
  reorderLevel: number;
}

export interface DashboardMetrics {
  kpis: DashboardKpi;
  revenueTrend: RevenueMonthlyTrend[];
  wardBedDistribution: WardBedDistribution[];
  esiDistribution: EsiDistribution[];
  todayAppointments: DashboardAppointment[];
  activeEmergencyQueue: DashboardTriage[];
  lowStockAlerts: DashboardDrugAlert[];
}
