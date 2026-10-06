import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { ReportsService } from './data-access/reports.service';
import { ExecutiveDashboardMetrics, MonthlyRevenueItem, AppointmentStatusCount, TopItemCount } from './data-access/reports.model';

@Component({
  selector: 'app-reports-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
  ],
  templateUrl: './reports-list.html',
})
export class ReportsListComponent implements OnInit {
  private reportsService = inject(ReportsService);

  // Global Math for template expressions
  Math = Math;

  // State Signals
  metrics = signal<ExecutiveDashboardMetrics | null>(null);
  loading = signal<boolean>(true);
  error = signal<string | null>(null);
  exportingCsv = signal<boolean>(false);

  // Reporting Period Filter
  selectedPeriod = signal<string>('All Time');
  showExportDropdown = signal<boolean>(false);

  // Computed Properties
  collectionRate = computed(() => {
    const m = this.metrics();
    if (!m || !m.totalRevenue || m.totalRevenue <= 0) return 0;
    return Math.round((m.totalCollected / m.totalRevenue) * 100);
  });

  maxMonthlyRevenue = computed(() => {
    const m = this.metrics();
    if (!m || !m.monthlyRevenueTrend || m.monthlyRevenueTrend.length === 0) return 1;
    return Math.max(...m.monthlyRevenueTrend.map((x) => Math.max(x.totalBilled || 0, x.totalCollected || 0)), 1);
  });

  ngOnInit(): void {
    this.loadExecutiveMetrics();
  }

  loadExecutiveMetrics(): void {
    this.loading.set(true);
    this.error.set(null);

    this.reportsService.getExecutiveDashboardMetrics().subscribe({
      next: (data) => {
        this.metrics.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set('Unable to load executive BI analytics data. Please try again.');
      },
    });
  }

  exportInvoicesCsv(): void {
    this.exportingCsv.set(true);
    this.showExportDropdown.set(false);

    this.reportsService.downloadInvoicesCsv().subscribe({
      next: (blob) => {
        this.exportingCsv.set(false);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `health-erp-invoices-report-${new Date().toISOString().substring(0, 10)}.csv`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        this.exportingCsv.set(false);
      },
    });
  }

  toggleExportDropdown(): void {
    this.showExportDropdown.set(!this.showExportDropdown());
  }

  // Formatting & Chart Helpers
  getBarHeightPercent(val: number): number {
    const max = this.maxMonthlyRevenue();
    if (!val || val <= 0 || !max) return 0;
    return Math.max(Math.min((val / max) * 100, 100), 4);
  }

  getPercent(count: number, total: number): number {
    if (!total || total <= 0) return 0;
    return Math.round((count / total) * 100);
  }

  getMaxTopCount(items: TopItemCount[]): number {
    if (!items || items.length === 0) return 1;
    return Math.max(...items.map((i) => i.count || 0), 1);
  }

  getTopItemWidthPercent(count: number, items: TopItemCount[]): number {
    const max = this.getMaxTopCount(items);
    if (!count || count <= 0 || !max) return 0;
    return Math.max(Math.min((count / max) * 100, 100), 6);
  }

  formatShortCurrency(val: number): string {
    if (!val || val === 0) return '$0';
    if (val >= 1000000) return `$${(val / 1000000).toFixed(1)}M`;
    if (val >= 1000) return `$${(val / 1000).toFixed(1)}K`;
    return `$${val.toFixed(0)}`;
  }
}
