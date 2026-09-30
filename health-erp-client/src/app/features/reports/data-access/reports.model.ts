export interface ExecutiveDashboardMetrics {
  totalRevenue: number;
  totalCollected: number;
  totalOutstandingBalance: number;
  totalPatients: number;
  totalAppointments: number;
  totalPrescriptionsIssued: number;
  totalLabTestsExecuted: number;
  monthlyRevenueTrend: MonthlyRevenueItem[];
  appointmentStatusBreakdown: AppointmentStatusCount[];
  topPrescribedDrugs: TopItemCount[];
  topLabTests: TopItemCount[];
}

export interface MonthlyRevenueItem {
  monthName: string;
  totalBilled: number;
  totalCollected: number;
}

export interface AppointmentStatusCount {
  status: string;
  count: number;
}

export interface TopItemCount {
  name: string;
  count: number;
}
