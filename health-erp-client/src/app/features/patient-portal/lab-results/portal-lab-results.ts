import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PatientPortalService, PatientLabResultDto } from '../../../core/services/patient-portal.service';

@Component({
  selector: 'app-portal-lab-results',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="space-y-6">

      <!-- PAGE HEADER -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 class="text-2xl font-bold text-[#0F172A] tracking-tight">My Lab Results</h2>
          <p class="text-sm text-[#64748B] mt-0.5">Released laboratory diagnostic test results and reference values.</p>
        </div>
      </div>

      <!-- LOADING SKELETON -->
      <div *ngIf="isLoading()" class="space-y-4">
        <div *ngFor="let i of [1,2,3]" class="bg-white p-5 rounded-xl border border-slate-200 animate-pulse space-y-3">
          <div class="h-5 bg-slate-200 rounded w-1/3"></div>
          <div class="h-3 bg-slate-200 rounded w-1/2"></div>
        </div>
      </div>

      <!-- LAB RESULTS CARDS LIST -->
      <div *ngIf="!isLoading() && labResults().length > 0" class="space-y-4">
        <div
          *ngFor="let lab of labResults()"
          class="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs hover:border-teal-300 transition-all space-y-4"
        >
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-slate-100 pb-3">
            <div class="flex items-center gap-3">
              <div
                class="w-10 h-10 rounded-xl flex items-center justify-center text-lg shrink-0 border"
                [ngClass]="lab.isAbnormal ? 'bg-amber-50 text-amber-600 border-amber-200' : 'bg-teal-50 text-[#0F766E] border-teal-200'"
              >
                <i class="pi pi-file"></i>
              </div>
              <div>
                <div class="flex items-center gap-2">
                  <h3 class="text-base font-bold text-[#0F172A] leading-tight">{{ lab.testName }}</h3>
                  <span class="text-[11px] font-semibold text-slate-500 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
                    {{ lab.testCode }}
                  </span>
                </div>
                <span class="text-xs text-slate-500 font-medium">Ordered by {{ lab.orderingDoctorName }} on {{ lab.orderedDateUtc | date:'mediumDate' }}</span>
              </div>
            </div>

            <div>
              <span
                class="px-3 py-1 rounded-full text-xs font-semibold inline-flex items-center gap-1.5"
                [ngClass]="lab.isAbnormal ? 'bg-amber-50 text-amber-700 border border-amber-200' : 'bg-emerald-50 text-emerald-700 border border-emerald-200'"
              >
                <span class="w-1.5 h-1.5 rounded-full" [ngClass]="lab.isAbnormal ? 'bg-amber-500' : 'bg-emerald-500'"></span>
                {{ lab.isAbnormal ? 'Outside Reference Range' : 'Normal / Released' }}
              </span>
            </div>
          </div>

          <!-- RESULT VALUES GRID -->
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs bg-slate-50/70 p-4 rounded-xl border border-slate-200/60">
            <div>
              <span class="text-slate-400 block text-[11px] font-medium uppercase tracking-wider mb-0.5">Test Result Value</span>
              <span class="font-extrabold text-base text-[#0F172A]">{{ lab.resultValue || 'Report Pending' }}</span>
              <span *ngIf="lab.unit" class="text-xs font-normal text-slate-500 ml-1">{{ lab.unit }}</span>
            </div>

            <div>
              <span class="text-slate-400 block text-[11px] font-medium uppercase tracking-wider mb-0.5">Reference Range</span>
              <span class="font-semibold text-slate-700 text-sm">{{ lab.referenceRange || 'Standard Range' }}</span>
            </div>

            <div>
              <span class="text-slate-400 block text-[11px] font-medium uppercase tracking-wider mb-0.5">Completion Date</span>
              <span class="font-semibold text-slate-700 text-sm">
                {{ lab.completedDateUtc ? (lab.completedDateUtc | date:'mediumDate') : 'Completed' }}
              </span>
            </div>
          </div>

          <div *ngIf="lab.isAbnormal" class="text-xs text-amber-800 bg-amber-50/70 p-3 rounded-xl border border-amber-200/60 flex items-start gap-2">
            <i class="pi pi-exclamation-triangle text-amber-600 text-sm mt-0.5 shrink-0"></i>
            <span>This result is outside standard reference limits. Please consult your physician for clinical discussion.</span>
          </div>
        </div>
      </div>

      <!-- EMPTY STATE -->
      <div *ngIf="!isLoading() && labResults().length === 0" class="bg-white p-12 rounded-2xl border border-slate-200 text-center max-w-md mx-auto my-8">
        <div class="w-14 h-14 rounded-full bg-slate-100 text-slate-400 flex items-center justify-center mx-auto mb-4 text-2xl">
          <i class="pi pi-file"></i>
        </div>
        <h3 class="text-base font-bold text-[#0F172A] mb-1">No lab results available</h3>
        <p class="text-xs text-slate-500">
          Only completed and released diagnostic lab test results will be displayed here.
        </p>
      </div>

    </div>
  `,
})
export class PortalLabResultsComponent implements OnInit {
  private portalService = inject(PatientPortalService);

  isLoading = signal(true);
  labResults = signal<PatientLabResultDto[]>([]);

  ngOnInit(): void {
    this.loadLabResults();
  }

  loadLabResults(): void {
    this.isLoading.set(true);
    // Strict requirement: Calls getLabResults() without passing any parameters or CompletedOnly override
    this.portalService.getLabResults().subscribe({
      next: (data) => {
        this.labResults.set(data || []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }
}
