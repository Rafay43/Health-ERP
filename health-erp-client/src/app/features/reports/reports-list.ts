import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { TableModule } from 'primeng/table';

import { ReportsService } from './data-access/reports.service';
import { ExecutiveDashboardMetrics } from './data-access/reports.model';

@Component({
  selector: 'app-reports-list',
  standalone: true,
  imports: [
    CommonModule,
    ButtonModule,
    TagModule,
    TooltipModule,
    TableModule,
  ],
  template: `
    <div class="flex flex-col gap-6" *ngIf="metrics() as m; else loadingState">
      <!-- Page Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Executive BI Analytics & Revenue Reports</h1>
          <p class="text-sm text-[var(--color-text-secondary)]">Real-time enterprise financial performance, patient visit statistics, and CSV export portal</p>
        </div>
        <div class="flex items-center gap-2">
          <button
            pButton
            icon="pi pi-download"
            label="Export Invoices CSV"
            class="p-button-primary"
            (click)="exportInvoicesCsv()"
          ></button>
        </div>
      </div>

      <!-- Financial KPI Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-emerald-100 text-emerald-600 rounded-lg">
            <i class="pi pi-dollar text-2xl"></i>
          </div>
          <div>
            <p class="text-xs font-bold text-[var(--color-text-secondary)] uppercase">Gross Billed Revenue</p>
            <h3 class="text-2xl font-black text-slate-900">$ {{ m.totalRevenue | number:'1.2-2' }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-blue-100 text-blue-600 rounded-lg">
            <i class="pi pi-check-circle text-2xl"></i>
          </div>
          <div>
            <p class="text-xs font-bold text-[var(--color-text-secondary)] uppercase">Total Cash Collected</p>
            <h3 class="text-2xl font-black text-emerald-600">$ {{ m.totalCollected | number:'1.2-2' }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-amber-100 text-amber-600 rounded-lg">
            <i class="pi pi-clock text-2xl"></i>
          </div>
          <div>
            <p class="text-xs font-bold text-[var(--color-text-secondary)] uppercase">Accounts Receivable</p>
            <h3 class="text-2xl font-black text-amber-600">$ {{ m.totalOutstandingBalance | number:'1.2-2' }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-indigo-100 text-indigo-600 rounded-lg">
            <i class="pi pi-users text-2xl"></i>
          </div>
          <div>
            <p class="text-xs font-bold text-[var(--color-text-secondary)] uppercase">Total Active Directory</p>
            <h3 class="text-2xl font-black text-indigo-600">{{ m.totalPatients }} Patients</h3>
          </div>
        </div>
      </div>

      <!-- Charts & Visual Analytics Section -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Monthly Revenue Trend (Comparison Bars) -->
        <div class="bg-[var(--color-surface)] p-6 rounded-xl border border-[var(--color-border)] shadow-xs flex flex-col gap-4">
          <div class="flex items-center justify-between border-b border-[var(--color-border)] pb-3">
            <h2 class="text-base font-bold text-slate-900 flex items-center gap-2">
              <i class="pi pi-chart-line text-emerald-600"></i> Monthly Billed vs Collected Revenue Trend
            </h2>
            <div class="flex items-center gap-4 text-xs font-semibold">
              <span class="flex items-center gap-1.5 text-slate-700">
                <span class="w-3 h-3 bg-indigo-500 rounded-xs"></span> Billed
              </span>
              <span class="flex items-center gap-1.5 text-slate-700">
                <span class="w-3 h-3 bg-emerald-500 rounded-xs"></span> Collected
              </span>
            </div>
          </div>

          <div class="flex items-end justify-between gap-3 h-48 pt-6 px-2 border-b border-slate-200">
            <div *ngFor="let item of m.monthlyRevenueTrend" class="flex-1 flex flex-col items-center gap-2 h-full justify-end">
              <div class="w-full flex items-end justify-center gap-1 h-36">
                <!-- Billed Bar -->
                <div
                  class="w-1/2 bg-indigo-500 hover:bg-indigo-600 rounded-t transition-all relative group"
                  [style.height.%]="getMaxPercent(item.totalBilled, m.monthlyRevenueTrend)"
                >
                  <div class="opacity-0 group-hover:opacity-100 absolute -top-8 left-1/2 -translate-x-1/2 bg-slate-900 text-white text-[10px] px-2 py-0.5 rounded whitespace-nowrap z-10">
                    Billed: $ {{ item.totalBilled | number:'1.0-0' }}
                  </div>
                </div>
                <!-- Collected Bar -->
                <div
                  class="w-1/2 bg-emerald-500 hover:bg-emerald-600 rounded-t transition-all relative group"
                  [style.height.%]="getMaxPercent(item.totalCollected, m.monthlyRevenueTrend)"
                >
                  <div class="opacity-0 group-hover:opacity-100 absolute -top-8 left-1/2 -translate-x-1/2 bg-slate-900 text-white text-[10px] px-2 py-0.5 rounded whitespace-nowrap z-10">
                    Paid: $ {{ item.totalCollected | number:'1.0-0' }}
                  </div>
                </div>
              </div>
              <span class="text-xs font-semibold text-slate-600">{{ item.monthName }}</span>
            </div>
          </div>
        </div>

        <!-- Appointment Status Breakdown -->
        <div class="bg-[var(--color-surface)] p-6 rounded-xl border border-[var(--color-border)] shadow-xs flex flex-col gap-4">
          <div class="flex items-center justify-between border-b border-[var(--color-border)] pb-3">
            <h2 class="text-base font-bold text-slate-900 flex items-center gap-2">
              <i class="pi pi-calendar text-blue-600"></i> Appointment Lifecycle Status Breakdown
            </h2>
            <span class="text-xs font-bold text-slate-500">Total Visits: {{ m.totalAppointments }}</span>
          </div>

          <div class="flex flex-col gap-3 py-2">
            <div *ngFor="let status of m.appointmentStatusBreakdown" class="flex flex-col gap-1">
              <div class="flex justify-between text-xs font-bold">
                <span class="text-slate-800">{{ status.status }}</span>
                <span class="text-slate-600">{{ status.count }} appointments ({{ getPercent(status.count, m.totalAppointments) }}%)</span>
              </div>
              <div class="w-full bg-slate-100 h-2.5 rounded-full overflow-hidden">
                <div
                  class="h-full rounded-full transition-all"
                  [ngClass]="{
                    'bg-emerald-500': status.status === 'Completed',
                    'bg-blue-500': status.status === 'Scheduled' || status.status === 'Confirmed',
                    'bg-rose-500': status.status === 'Cancelled' || status.status === 'NoShow'
                  }"
                  [style.width.%]="getPercent(status.count, m.totalAppointments)"
                ></div>
              </div>
            </div>

            <div *ngIf="m.appointmentStatusBreakdown.length === 0" class="text-xs text-slate-400 italic py-6 text-center">
              No appointment history available.
            </div>
          </div>
        </div>
      </div>

      <!-- Top Medications & Lab Tests Data Tables -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Top Prescribed Drugs -->
        <div class="bg-[var(--color-surface)] p-6 rounded-xl border border-[var(--color-border)] shadow-xs flex flex-col gap-4">
          <div class="flex items-center justify-between border-b border-[var(--color-border)] pb-3">
            <h2 class="text-base font-bold text-slate-900 flex items-center gap-2">
              <i class="pi pi-briefcase text-purple-600"></i> Top Prescribed Medications
            </h2>
            <span class="text-xs text-slate-500">Total Issued: {{ m.totalPrescriptionsIssued }}</span>
          </div>

          <div class="flex flex-col gap-3">
            <div *ngFor="let drug of m.topPrescribedDrugs; let i = index" class="flex items-center justify-between p-3 bg-slate-50 border border-slate-200 rounded-lg">
              <div class="flex items-center gap-3">
                <span class="w-6 h-6 rounded-full bg-indigo-100 text-indigo-700 font-bold text-xs flex items-center gap-1 justify-center">
                  #{{ i + 1 }}
                </span>
                <span class="font-bold text-slate-900 text-sm">{{ drug.name }}</span>
              </div>
              <span class="px-2.5 py-1 bg-indigo-50 text-indigo-700 font-bold rounded text-xs">
                {{ drug.count }} orders
              </span>
            </div>
            <div *ngIf="m.topPrescribedDrugs.length === 0" class="text-xs text-slate-400 italic py-4 text-center">
              No prescription data recorded.
            </div>
          </div>
        </div>

        <!-- Top Lab Tests Executed -->
        <div class="bg-[var(--color-surface)] p-6 rounded-xl border border-[var(--color-border)] shadow-xs flex flex-col gap-4">
          <div class="flex items-center justify-between border-b border-[var(--color-border)] pb-3">
            <h2 class="text-base font-bold text-slate-900 flex items-center gap-2">
              <i class="pi pi-compass text-rose-600"></i> Top Diagnostic Lab Tests Executed
            </h2>
            <span class="text-xs text-slate-500">Total Executed: {{ m.totalLabTestsExecuted }}</span>
          </div>

          <div class="flex flex-col gap-3">
            <div *ngFor="let lab of m.topLabTests; let i = index" class="flex items-center justify-between p-3 bg-slate-50 border border-slate-200 rounded-lg">
              <div class="flex items-center gap-3">
                <span class="w-6 h-6 rounded-full bg-rose-100 text-rose-700 font-bold text-xs flex items-center gap-1 justify-center">
                  #{{ i + 1 }}
                </span>
                <span class="font-bold text-slate-900 text-sm">{{ lab.name }}</span>
              </div>
              <span class="px-2.5 py-1 bg-rose-50 text-rose-700 font-bold rounded text-xs">
                {{ lab.count }} executed
              </span>
            </div>
            <div *ngIf="m.topLabTests.length === 0" class="text-xs text-slate-400 italic py-4 text-center">
              No diagnostic test data recorded.
            </div>
          </div>
        </div>
      </div>
    </div>

    <ng-template #loadingState>
      <div class="flex items-center justify-center py-20 text-slate-500 gap-3">
        <i class="pi pi-spin pi-spinner text-2xl"></i>
        <span>Calculating enterprise executive BI analytics...</span>
      </div>
    </ng-template>
  `
})
export class ReportsListComponent implements OnInit {
  private reportsService = inject(ReportsService);

  metrics = signal<ExecutiveDashboardMetrics | null>(null);

  ngOnInit(): void {
    this.reportsService.getExecutiveDashboardMetrics().subscribe({
      next: (data) => this.metrics.set(data),
      error: () => {}
    });
  }

  exportInvoicesCsv(): void {
    this.reportsService.downloadInvoicesCsv().subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `invoices-report-${new Date().toISOString().substring(0, 10)}.csv`;
        a.click();
        window.URL.revokeObjectURL(url);
      }
    });
  }

  getMaxPercent(val: number, list: any[]): number {
    const max = Math.max(...list.map(x => Math.max(x.totalBilled, x.totalCollected)), 1);
    return Math.max(Math.min((val / max) * 100, 100), 5);
  }

  getPercent(count: number, total: number): number {
    if (!total) return 0;
    return Math.round((count / total) * 100);
  }
}
