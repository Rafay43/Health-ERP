import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PatientPortalService, PatientPrescriptionDto } from '../../../core/services/patient-portal.service';

@Component({
  selector: 'app-portal-prescriptions',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="space-y-6">

      <!-- PAGE HEADER -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 class="text-2xl font-bold text-[#0F172A] tracking-tight">My Prescriptions</h2>
          <p class="text-sm text-[#64748B] mt-0.5">Read-only record of prescribed medications and dosage instructions.</p>
        </div>
      </div>

      <!-- FILTER TABS -->
      <div class="flex items-center gap-2 border-b border-slate-200 pb-3 overflow-x-auto">
        <button
          (click)="activeTab.set('all')"
          class="px-3.5 py-1.5 rounded-full text-xs font-semibold transition-colors cursor-pointer whitespace-nowrap"
          [ngClass]="activeTab() === 'all' ? 'bg-[#0F766E] text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
        >
          All Medications
        </button>
        <button
          (click)="activeTab.set('active')"
          class="px-3.5 py-1.5 rounded-full text-xs font-semibold transition-colors cursor-pointer whitespace-nowrap"
          [ngClass]="activeTab() === 'active' ? 'bg-[#0F766E] text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
        >
          Active Prescriptions
        </button>
        <button
          (click)="activeTab.set('completed')"
          class="px-3.5 py-1.5 rounded-full text-xs font-semibold transition-colors cursor-pointer whitespace-nowrap"
          [ngClass]="activeTab() === 'completed' ? 'bg-[#0F766E] text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
        >
          Historical
        </button>
      </div>

      <!-- LOADING SKELETON -->
      <div *ngIf="isLoading()" class="space-y-4">
        <div *ngFor="let i of [1,2,3]" class="bg-white p-5 rounded-xl border border-slate-200 animate-pulse space-y-3">
          <div class="h-5 bg-slate-200 rounded w-1/3"></div>
          <div class="h-3 bg-slate-200 rounded w-1/2"></div>
        </div>
      </div>

      <!-- PRESCRIPTIONS LIST -->
      <div *ngIf="!isLoading() && filteredPrescriptions().length > 0" class="space-y-4">
        <div
          *ngFor="let rx of filteredPrescriptions()"
          class="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs hover:border-teal-300 transition-all space-y-3"
        >
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-slate-100 pb-3">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-xl bg-sky-50 text-sky-600 flex items-center justify-center text-lg shrink-0 border border-sky-100">
                <i class="pi pi-sliders-h"></i>
              </div>
              <div>
                <h3 class="text-base font-bold text-[#0F172A] leading-tight">{{ rx.medicationName }}</h3>
                <span class="text-xs text-slate-500 font-medium">Prescribed on {{ rx.prescribedDateUtc | date:'mediumDate' }} by {{ rx.doctorName?.trim() || 'Not recorded' }}</span>
              </div>
            </div>

            <div>
              <span
                class="px-3 py-1 rounded-full text-xs font-semibold inline-flex items-center gap-1.5"
                [ngClass]="getStatusBadgeClass(rx.status)"
              >
                <span class="w-1.5 h-1.5 rounded-full" [ngClass]="getStatusDotClass(rx.status)"></span>
                {{ rx.status }}
              </span>
            </div>
          </div>

          <!-- DOSAGE & INSTRUCTIONS GRID -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs bg-slate-50/60 p-3 rounded-xl border border-slate-200/60">
            <div>
              <span class="text-slate-400 block text-[11px] font-medium">Dosage</span>
              <span class="font-bold text-[#0F172A]">{{ rx.dosage || 'Not recorded' }}</span>
            </div>
            <div>
              <span class="text-slate-400 block text-[11px] font-medium">Frequency</span>
              <span class="font-bold text-[#0F172A]">{{ rx.frequency || 'Not recorded' }}</span>
            </div>
            <div>
              <span class="text-slate-400 block text-[11px] font-medium">Duration</span>
              <span class="font-bold text-[#0F172A]">{{ rx.durationDays != null && rx.durationDays > 0 ? rx.durationDays + (rx.durationDays === 1 ? ' day' : ' days') : 'N/A' }}</span>
            </div>
            <div>
              <span class="text-slate-400 block text-[11px] font-medium">Status</span>
              <span class="font-bold text-[#0F766E]">{{ rx.status }}</span>
            </div>
          </div>

          <div *ngIf="rx.instructions" class="text-xs text-slate-600 bg-teal-50/50 p-3 rounded-xl border border-teal-100/60 flex items-start gap-2">
            <i class="pi pi-info-circle text-[#0F766E] text-sm mt-0.5 shrink-0"></i>
            <span><strong class="text-[#0F172A]">Instructions:</strong> {{ rx.instructions }}</span>
          </div>
        </div>
      </div>

      <!-- EMPTY STATE -->
      <div *ngIf="!isLoading() && filteredPrescriptions().length === 0" class="bg-white p-12 rounded-2xl border border-slate-200 text-center max-w-md mx-auto my-8">
        <div class="w-14 h-14 rounded-full bg-slate-100 text-slate-400 flex items-center justify-center mx-auto mb-4 text-2xl">
          <i class="pi pi-sliders-h"></i>
        </div>
        <h3 class="text-base font-bold text-[#0F172A] mb-1">No prescriptions found</h3>
        <p class="text-xs text-slate-500">
          No medication prescriptions match the selected filter.
        </p>
      </div>

    </div>
  `,
})
export class PortalPrescriptionsComponent implements OnInit {
  private portalService = inject(PatientPortalService);

  isLoading = signal(true);
  prescriptions = signal<PatientPrescriptionDto[]>([]);
  activeTab = signal<'all' | 'active' | 'completed'>('all');

  filteredPrescriptions = computed(() => {
    const list = this.prescriptions();
    const tab = this.activeTab();

    if (tab === 'active') {
      return list.filter(r => r.status?.toLowerCase() === 'pending' || r.status?.toLowerCase() === 'dispensed');
    }
    if (tab === 'completed') {
      return list.filter(r => r.status?.toLowerCase() === 'cancelled');
    }
    return list;
  });

  ngOnInit(): void {
    this.loadPrescriptions();
  }

  loadPrescriptions(): void {
    this.isLoading.set(true);
    this.portalService.getPrescriptions().subscribe({
      next: (data) => {
        this.prescriptions.set(data || []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }

  getStatusBadgeClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'active':
      case 'dispensed':
        return 'bg-[#0F766E]/10 text-[#0F766E] border border-teal-200';
      case 'completed':
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      default:
        return 'bg-slate-100 text-slate-700 border border-slate-200';
    }
  }

  getStatusDotClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'active':
      case 'dispensed':
        return 'bg-[#0F766E]';
      case 'completed':
        return 'bg-emerald-500';
      default:
        return 'bg-slate-400';
    }
  }
}
