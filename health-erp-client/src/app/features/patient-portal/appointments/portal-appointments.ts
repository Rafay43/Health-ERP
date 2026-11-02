import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PatientPortalService, PatientAppointmentDto } from '../../../core/services/patient-portal.service';

@Component({
  selector: 'app-portal-appointments',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="space-y-6">

      <!-- PAGE HEADER -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 class="text-2xl font-bold text-[#0F172A] tracking-tight">My Appointments</h2>
          <p class="text-sm text-[#64748B] mt-0.5">View your upcoming and historical hospital appointments.</p>
        </div>
      </div>

      <!-- FILTER TABS -->
      <div class="flex items-center gap-2 border-b border-slate-200 pb-3 overflow-x-auto">
        <button
          (click)="activeTab.set('all')"
          class="px-3.5 py-1.5 rounded-full text-xs font-semibold transition-colors cursor-pointer whitespace-nowrap"
          [ngClass]="activeTab() === 'all' ? 'bg-[#0F766E] text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
        >
          All Appointments
        </button>
        <button
          (click)="activeTab.set('upcoming')"
          class="px-3.5 py-1.5 rounded-full text-xs font-semibold transition-colors cursor-pointer whitespace-nowrap"
          [ngClass]="activeTab() === 'upcoming' ? 'bg-[#0F766E] text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
        >
          Upcoming
        </button>
        <button
          (click)="activeTab.set('past')"
          class="px-3.5 py-1.5 rounded-full text-xs font-semibold transition-colors cursor-pointer whitespace-nowrap"
          [ngClass]="activeTab() === 'past' ? 'bg-[#0F766E] text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
        >
          Past Visits
        </button>
      </div>

      <!-- LOADING SKELETON -->
      <div *ngIf="isLoading()" class="space-y-4">
        <div *ngFor="let i of [1,2,3]" class="bg-white p-5 rounded-xl border border-slate-200 animate-pulse flex items-center justify-between">
          <div class="space-y-2 w-1/2">
            <div class="h-4 bg-slate-200 rounded w-3/4"></div>
            <div class="h-3 bg-slate-200 rounded w-1/2"></div>
          </div>
          <div class="h-6 bg-slate-200 rounded w-20"></div>
        </div>
      </div>

      <!-- APPOINTMENT CARDS LIST -->
      <div *ngIf="!isLoading() && filteredAppointments().length > 0" class="space-y-4">
        <div
          *ngFor="let appt of filteredAppointments()"
          class="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs hover:border-teal-300 transition-all flex flex-col sm:flex-row sm:items-center justify-between gap-4"
        >
          <div class="flex items-start gap-4">
            <div class="w-12 h-12 rounded-xl bg-teal-50 text-[#0F766E] flex flex-col items-center justify-center shrink-0 border border-teal-100">
              <span class="text-xs font-bold leading-none uppercase">{{ appt.startDateTimeUtc | date:'MMM' }}</span>
              <span class="text-base font-extrabold leading-tight">{{ appt.startDateTimeUtc | date:'dd' }}</span>
            </div>

            <div>
              <div class="flex items-center gap-2 mb-1">
                <h3 class="text-base font-bold text-[#0F172A]">{{ appt.reason || 'General Consultation' }}</h3>
              </div>
              
              <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-500 font-medium">
                <span class="flex items-center gap-1.5 text-slate-700 font-semibold">
                  <i class="pi pi-user text-[#0F766E]"></i> {{ appt.doctorName?.trim() || 'Not recorded' }}
                </span>
                <span class="flex items-center gap-1.5">
                  <i class="pi pi-building text-slate-400"></i> {{ appt.department }}
                </span>
                <span class="flex items-center gap-1.5 text-teal-700 font-medium">
                  <i class="pi pi-clock text-teal-600"></i> {{ appt.startDateTimeUtc | date:'shortTime' }}
                </span>
              </div>
            </div>
          </div>

          <div class="flex items-center justify-between sm:justify-end gap-3 pt-3 sm:pt-0 border-t sm:border-t-0 border-slate-100">
            <span
              class="px-3 py-1 rounded-full text-xs font-semibold inline-flex items-center gap-1.5"
              [ngClass]="getStatusBadgeClass(appt.status)"
            >
              <span class="w-1.5 h-1.5 rounded-full" [ngClass]="getStatusDotClass(appt.status)"></span>
              {{ appt.status }}
            </span>
          </div>
        </div>
      </div>

      <!-- EMPTY STATE -->
      <div *ngIf="!isLoading() && filteredAppointments().length === 0" class="bg-white p-12 rounded-2xl border border-slate-200 text-center max-w-md mx-auto my-8">
        <div class="w-14 h-14 rounded-full bg-slate-100 text-slate-400 flex items-center justify-center mx-auto mb-4 text-2xl">
          <i class="pi pi-calendar"></i>
        </div>
        <h3 class="text-base font-bold text-[#0F172A] mb-1">No appointments found</h3>
        <p class="text-xs text-slate-500">
          {{ activeTab() === 'upcoming' ? 'You have no upcoming appointments scheduled.' : 'No appointment records available.' }}
        </p>
      </div>

    </div>
  `,
})
export class PortalAppointmentsComponent implements OnInit {
  private portalService = inject(PatientPortalService);

  isLoading = signal(true);
  appointments = signal<PatientAppointmentDto[]>([]);
  activeTab = signal<'all' | 'upcoming' | 'past'>('all');

  filteredAppointments = computed(() => {
    const list = this.appointments();
    const tab = this.activeTab();
    const now = new Date().toISOString();

    if (tab === 'upcoming') {
      return list.filter(a => a.startDateTimeUtc >= now || a.status === 'Scheduled' || a.status === 'Confirmed');
    }
    if (tab === 'past') {
      return list.filter(a => a.startDateTimeUtc < now || a.status === 'Completed' || a.status === 'Cancelled');
    }
    return list;
  });

  ngOnInit(): void {
    this.loadAppointments();
  }

  loadAppointments(): void {
    this.isLoading.set(true);
    this.portalService.getAppointments().subscribe({
      next: (data) => {
        this.appointments.set(data || []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }

  getStatusBadgeClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'scheduled':
      case 'confirmed':
        return 'bg-teal-50 text-[#0F766E] border border-teal-200/60';
      case 'completed':
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200/60';
      case 'cancelled':
      case 'noshow':
        return 'bg-rose-50 text-rose-700 border border-rose-200/60';
      default:
        return 'bg-slate-100 text-slate-700 border border-slate-200';
    }
  }

  getStatusDotClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'scheduled':
      case 'confirmed':
        return 'bg-[#0F766E]';
      case 'completed':
        return 'bg-emerald-500';
      case 'cancelled':
      case 'noshow':
        return 'bg-rose-500';
      default:
        return 'bg-slate-400';
    }
  }
}
