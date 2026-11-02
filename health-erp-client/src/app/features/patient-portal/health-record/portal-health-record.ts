import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PatientPortalService, PatientHealthRecordDto } from '../../../core/services/patient-portal.service';

@Component({
  selector: 'app-portal-health-record',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="space-y-8">

      <!-- PAGE HEADER -->
      <div>
        <h2 class="text-2xl font-bold text-[#0F172A] tracking-tight">My Health Record</h2>
        <p class="text-sm text-[#64748B] mt-0.5">Read-only overview of recorded vitals and clinical encounters.</p>
      </div>

      <!-- LOADING SKELETON -->
      <div *ngIf="isLoading()" class="space-y-6">
        <div class="bg-white p-6 rounded-2xl border border-slate-200 animate-pulse space-y-4">
          <div class="h-5 bg-slate-200 rounded w-1/3"></div>
          <div class="h-20 bg-slate-100 rounded w-full"></div>
        </div>
      </div>

      <div *ngIf="!isLoading()" class="space-y-8">

        <!-- SECTION 1: RECENT VITALS -->
        <div class="bg-white rounded-2xl border border-slate-200/80 shadow-xs p-6 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-100 pb-3">
            <div class="flex items-center gap-2">
              <div class="w-8 h-8 rounded-lg bg-rose-50 text-rose-600 flex items-center justify-center text-sm">
                <i class="pi pi-heart-fill"></i>
              </div>
              <h3 class="text-base font-bold text-[#0F172A]">Vital Signs Summary</h3>
            </div>
            <span class="text-xs text-slate-400 font-medium">Recorded Vitals</span>
          </div>

          <!-- VITALS GRID OR TABLE -->
          <div *ngIf="healthRecord()?.latestVitals as vital; else noVitals" class="space-y-4">
            <div class="bg-slate-50/70 p-4 rounded-xl border border-slate-200/60 space-y-3">
              <div class="flex items-center justify-between border-b border-slate-200/60 pb-2">
                <span class="text-xs font-bold text-[#0F172A]">
                  Measured on {{ vital.recordedDateUtc | date:'mediumDate' }} at {{ vital.recordedDateUtc | date:'shortTime' }}
                </span>
                <span class="text-[11px] font-semibold text-[#0F766E] bg-teal-50 px-2 py-0.5 rounded-full border border-teal-200/50">
                  Verified Vitals
                </span>
              </div>

              <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs">
                <div class="bg-white p-3 rounded-lg border border-slate-200/60">
                  <span class="text-slate-400 block text-[11px] mb-0.5">Blood Pressure</span>
                  <span class="font-bold text-[#0F172A] text-sm">
                    {{ vital.bloodPressure?.trim() ? vital.bloodPressure + ' mmHg' : 'N/A' }}
                  </span>
                </div>

                <div class="bg-white p-3 rounded-lg border border-slate-200/60">
                  <span class="text-slate-400 block text-[11px] mb-0.5">Pulse Rate</span>
                  <span class="font-bold text-[#0F172A] text-sm">
                    {{ vital.heartRateBpm != null ? vital.heartRateBpm + ' bpm' : 'N/A' }}
                  </span>
                </div>

                <div class="bg-white p-3 rounded-lg border border-slate-200/60">
                  <span class="text-slate-400 block text-[11px] mb-0.5">Temperature</span>
                  <span class="font-bold text-[#0F172A] text-sm">
                    {{ vital.temperatureCelsius != null ? vital.temperatureCelsius + ' °C' : 'N/A' }}
                  </span>
                </div>

                <div class="bg-white p-3 rounded-lg border border-slate-200/60">
                  <span class="text-slate-400 block text-[11px] mb-0.5">Weight</span>
                  <span class="font-bold text-[#0F172A] text-sm">
                    {{ vital.weightKg != null ? vital.weightKg + ' kg' : 'N/A' }}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <ng-template #noVitals>
            <div class="py-8 text-center text-slate-400 text-xs font-medium">
              No recent vital signs recorded in your medical history.
            </div>
          </ng-template>
        </div>

        <!-- SECTION 2: CLINICAL VISITS / ENCOUNTERS -->
        <div class="bg-white rounded-2xl border border-slate-200/80 shadow-xs p-6 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-100 pb-3">
            <div class="flex items-center gap-2">
              <div class="w-8 h-8 rounded-lg bg-sky-50 text-sky-600 flex items-center justify-center text-sm">
                <i class="pi pi-user text-sky-600"></i>
              </div>
              <h3 class="text-base font-bold text-[#0F172A]">Clinical Consultations</h3>
            </div>
            <span class="text-xs text-slate-400 font-medium">Primary Diagnoses</span>
          </div>

          <div *ngIf="healthRecord()?.encounters?.length; else noEncounters" class="space-y-4">
            <div
              *ngFor="let enc of healthRecord()?.encounters"
              class="bg-slate-50/70 p-5 rounded-xl border border-slate-200/60 space-y-2"
            >
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-1 border-b border-slate-200/60 pb-2">
                <div>
                  <span class="text-sm font-bold text-[#0F172A] block">{{ enc.doctorName?.trim() || 'Not recorded' }}</span>
                  <span class="text-xs text-slate-500 font-medium">Visit Date: {{ enc.noteDateUtc | date:'mediumDate' }}</span>
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-1 text-xs">
                <div class="bg-white p-3 rounded-lg border border-slate-200/60">
                  <span class="text-slate-400 font-semibold block text-[11px] uppercase tracking-wider mb-1">Chief Complaint</span>
                  <span class="font-medium text-[#0F172A]">{{ enc.chiefComplaint }}</span>
                </div>

                <div class="bg-white p-3 rounded-lg border border-slate-200/60">
                  <span class="text-slate-400 font-semibold block text-[11px] uppercase tracking-wider mb-1">Primary Diagnosis</span>
                  <span class="font-semibold text-teal-800">{{ enc.diagnosis }}</span>
                </div>
              </div>
            </div>
          </div>

          <ng-template #noEncounters>
            <div class="py-8 text-center text-slate-400 text-xs font-medium">
              No clinical consultation history recorded.
            </div>
          </ng-template>
        </div>

      </div>

    </div>
  `,
})
export class PortalHealthRecordComponent implements OnInit {
  private portalService = inject(PatientPortalService);

  isLoading = signal(true);
  healthRecord = signal<PatientHealthRecordDto | null>(null);

  ngOnInit(): void {
    this.loadHealthRecord();
  }

  loadHealthRecord(): void {
    this.isLoading.set(true);
    this.portalService.getHealthRecord().subscribe({
      next: (data) => {
        this.healthRecord.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }
}
