import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PatientPortalService, PatientDashboardDto, PatientPortalProfileDto } from '../../../core/services/patient-portal.service';

@Component({
  selector: 'app-portal-overview',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="space-y-6">

      <!-- WELCOME BANNER -->
      <div class="bg-gradient-to-r from-[#0F766E] to-[#115E59] rounded-2xl p-6 sm:p-8 text-white shadow-md relative overflow-hidden">
        <div class="absolute -right-6 -bottom-6 w-48 h-48 bg-white/10 rounded-full blur-2xl pointer-events-none"></div>
        <div class="relative z-10 max-w-2xl">
          <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-white/20 text-teal-50 border border-white/30 mb-3">
            <i class="pi pi-shield text-xs"></i> Patient Portal Active
          </span>
          <h2 class="text-2xl sm:text-3xl font-bold tracking-tight mb-2">
            Good day, {{ profile()?.firstName || 'Patient' }}
          </h2>
          <p class="text-teal-100 text-sm sm:text-base font-normal">
            Here's an overview of your health activity, recent vitals, upcoming appointments, and medical summaries.
          </p>
        </div>
      </div>

      <!-- LOADING SKELETON STATE -->
      <div *ngIf="isLoading()" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
        <div *ngFor="let i of [1,2,3,4]" class="bg-white p-6 rounded-2xl border border-slate-200 animate-pulse space-y-3">
          <div class="h-4 bg-slate-200 rounded w-1/2"></div>
          <div class="h-8 bg-slate-200 rounded w-3/4"></div>
          <div class="h-3 bg-slate-200 rounded w-1/3"></div>
        </div>
      </div>

      <!-- MAIN 4 SUMMARY METRIC CARDS -->
      <div *ngIf="!isLoading()" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">

        <!-- CARD 1: NEXT APPOINTMENT -->
        <div class="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs hover:border-teal-300 transition-all flex flex-col justify-between">
          <div>
            <div class="flex items-center justify-between mb-3">
              <span class="text-xs font-bold uppercase tracking-wider text-slate-400">Next Appointment</span>
              <div class="w-8 h-8 rounded-lg bg-teal-50 text-[#0F766E] flex items-center justify-center text-sm">
                <i class="pi pi-calendar"></i>
              </div>
            </div>

            <div *ngIf="dashboard()?.nextAppointment as appt; else noAppt">
              <p class="text-base font-bold text-[#0F172A] leading-tight mb-1">
                {{ appt.startDateTimeUtc | date:'mediumDate' }}
              </p>
              <p class="text-xs font-semibold text-[#0F766E] mb-2">
                {{ appt.startDateTimeUtc | date:'shortTime' }}
              </p>
              <p class="text-xs font-medium text-slate-600 truncate">
                <i class="pi pi-user text-[11px] mr-1 text-slate-400"></i>{{ appt.doctorName?.trim() || 'Not recorded' }}
              </p>
              <p class="text-[11px] text-slate-400 truncate">{{ appt.department }}</p>
            </div>

            <ng-template #noAppt>
              <div class="py-2">
                <p class="text-sm font-semibold text-slate-600">No upcoming appointments</p>
                <p class="text-xs text-slate-400 mt-1">Schedule details will appear here once booked.</p>
              </div>
            </ng-template>
          </div>

          <div class="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between">
            <a routerLink="/portal/appointments" class="text-xs font-semibold text-[#0F766E] hover:underline flex items-center gap-1">
              View appointments <i class="pi pi-arrow-right text-[10px]"></i>
            </a>
          </div>
        </div>

        <!-- CARD 2: LATEST VITALS -->
        <div class="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs hover:border-teal-300 transition-all flex flex-col justify-between">
          <div>
            <div class="flex items-center justify-between mb-3">
              <span class="text-xs font-bold uppercase tracking-wider text-slate-400">Latest Vitals</span>
              <div class="w-8 h-8 rounded-lg bg-rose-50 text-rose-600 flex items-center justify-center text-sm">
                <i class="pi pi-heart-fill"></i>
              </div>
            </div>

            <div *ngIf="dashboard()?.latestVitals as vitals; else noVitals">
              <div class="flex items-baseline gap-2 mb-1">
                <span class="text-xl font-bold text-[#0F172A]">
                  {{ vitals.bloodPressure?.trim() || 'N/A' }}
                </span>
                <span class="text-xs font-medium text-slate-500">mmHg</span>
              </div>
              <p class="text-xs font-medium text-slate-600">
                Pulse: <span class="font-semibold text-[#0F172A]">{{ vitals.heartRateBpm != null ? vitals.heartRateBpm + ' bpm' : 'N/A' }}</span>
              </p>
              <p class="text-[11px] text-slate-400 mt-1">
                Measured {{ vitals.recordedDateUtc | date:'shortDate' }}
              </p>
            </div>

            <ng-template #noVitals>
              <div class="py-2">
                <p class="text-sm font-semibold text-slate-600">No recent vitals available</p>
                <p class="text-xs text-slate-400 mt-1">Clinical measurements will show here.</p>
              </div>
            </ng-template>
          </div>

          <div class="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between">
            <a routerLink="/portal/health-record" class="text-xs font-semibold text-[#0F766E] hover:underline flex items-center gap-1">
              View health record <i class="pi pi-arrow-right text-[10px]"></i>
            </a>
          </div>
        </div>

        <!-- CARD 3: ACTIVE PRESCRIPTIONS -->
        <div class="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs hover:border-teal-300 transition-all flex flex-col justify-between">
          <div>
            <div class="flex items-center justify-between mb-3">
              <span class="text-xs font-bold uppercase tracking-wider text-slate-400">Active Prescriptions</span>
              <div class="w-8 h-8 rounded-lg bg-sky-50 text-sky-600 flex items-center justify-center text-sm">
                <i class="pi pi-sliders-h"></i>
              </div>
            </div>

            <div class="py-1">
              <div class="text-3xl font-bold text-[#0F172A] mb-1">
                {{ dashboard()?.activePrescriptionCount || 0 }}
              </div>
              <p class="text-xs text-slate-500">
                {{ (dashboard()?.activePrescriptionCount || 0) === 1 ? 'Current active medication' : 'Active medications on record' }}
              </p>
            </div>
          </div>

          <div class="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between">
            <a routerLink="/portal/prescriptions" class="text-xs font-semibold text-[#0F766E] hover:underline flex items-center gap-1">
              View prescriptions <i class="pi pi-arrow-right text-[10px]"></i>
            </a>
          </div>
        </div>

        <!-- CARD 4: OUTSTANDING BALANCE -->
        <div class="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs hover:border-teal-300 transition-all flex flex-col justify-between">
          <div>
            <div class="flex items-center justify-between mb-3">
              <span class="text-xs font-bold uppercase tracking-wider text-slate-400">Outstanding Balance</span>
              <div class="w-8 h-8 rounded-lg bg-amber-50 text-amber-600 flex items-center justify-center text-sm">
                <i class="pi pi-wallet"></i>
              </div>
            </div>

            <div class="py-1">
              <div class="text-2xl sm:text-3xl font-bold text-[#0F172A] mb-1">
                {{ (dashboard()?.outstandingBalance || 0) | currency:'USD':'symbol':'1.2-2' }}
              </div>
              <p class="text-xs font-medium text-[#64748B]">
                {{ (dashboard()?.outstandingBalance || 0) > 0 ? 'Pending invoice balance' : 'No balance due' }}
              </p>
            </div>
          </div>

          <div class="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between">
            <a routerLink="/portal/billing" class="text-xs font-semibold text-[#0F766E] hover:underline flex items-center gap-1">
              View billing statement <i class="pi pi-arrow-right text-[10px]"></i>
            </a>
          </div>
        </div>

      </div>

      <!-- QUICK PORTAL ACTION & NAVIGATION CARDS -->
      <div class="grid grid-cols-1 md:grid-cols-3 gap-5">
        <div class="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs">
          <div class="w-10 h-10 rounded-xl bg-teal-50 text-[#0F766E] flex items-center justify-center text-lg mb-3">
            <i class="pi pi-file text-[#0F766E]"></i>
          </div>
          <h3 class="text-base font-bold text-[#0F172A] mb-1">Laboratory Reports</h3>
          <p class="text-xs text-slate-500 mb-4 leading-relaxed">
            Review completed lab test results, reference ranges, and diagnostic reports.
          </p>
          <a routerLink="/portal/lab-results" class="inline-flex items-center text-xs font-semibold text-[#0F766E] hover:underline gap-1">
            Access lab results <i class="pi pi-arrow-right text-[10px]"></i>
          </a>
        </div>

        <div class="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs">
          <div class="w-10 h-10 rounded-xl bg-sky-50 text-sky-600 flex items-center justify-center text-lg mb-3">
            <i class="pi pi-heart text-sky-600"></i>
          </div>
          <h3 class="text-base font-bold text-[#0F172A] mb-1">Clinical Encounters</h3>
          <p class="text-xs text-slate-500 mb-4 leading-relaxed">
            View your consultation history, primary diagnoses, and clinical visit summaries.
          </p>
          <a routerLink="/portal/health-record" class="inline-flex items-center text-xs font-semibold text-[#0F766E] hover:underline gap-1">
            View medical record <i class="pi pi-arrow-right text-[10px]"></i>
          </a>
        </div>

        <div class="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs">
          <div class="w-10 h-10 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center text-lg mb-3">
            <i class="pi pi-user text-indigo-600"></i>
          </div>
          <h3 class="text-base font-bold text-[#0F172A] mb-1">Patient Profile</h3>
          <p class="text-xs text-slate-500 mb-4 leading-relaxed">
            Check your personal details, contact numbers, and registered medical record number.
          </p>
          <a routerLink="/portal/profile" class="inline-flex items-center text-xs font-semibold text-[#0F766E] hover:underline gap-1">
            View profile details <i class="pi pi-arrow-right text-[10px]"></i>
          </a>
        </div>
      </div>

    </div>
  `,
})
export class PortalOverviewComponent implements OnInit {
  private portalService = inject(PatientPortalService);

  isLoading = signal(true);
  dashboard = signal<PatientDashboardDto | null>(null);
  profile = signal<PatientPortalProfileDto | null>(null);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);

    this.portalService.getProfile().subscribe({
      next: (prof) => this.profile.set(prof),
      error: () => {},
    });

    this.portalService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }
}
