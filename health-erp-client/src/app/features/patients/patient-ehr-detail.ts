import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';

import { EhrService } from './data-access/ehr.service';
import { PatientEhr, EhrTimelineEvent } from './data-access/ehr.model';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-patient-ehr-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    TagModule,
    TooltipModule,
    InputTextModule,
    DialogModule,
    MessageModule,
  ],
  template: `
    <div class="flex flex-col gap-6" *ngIf="ehr() as p; else loadingState">
      <!-- Top Navigation & Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <a routerLink="/patients" pButton icon="pi pi-arrow-left" class="p-button-outlined p-button-secondary p-button-sm"></a>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">{{ p.firstName }} {{ p.lastName }}</h1>
              <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-100 text-indigo-800">
                MRN: {{ p.medicalRecordNumber }}
              </span>
            </div>
            <p class="text-sm text-[var(--color-text-secondary)]">
              {{ p.age }} yrs • {{ p.gender || 'Unspecified' }} • DOB: {{ p.dateOfBirth | date:'mediumDate' }}
            </p>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <button
            pButton
            icon="pi pi-plus"
            label="Record Encounter Note"
            class="p-button-primary"
            (click)="openAddNoteDialog()"
          ></button>
        </div>
      </div>

      <!-- Patient Vitals & Summary Widgets Grid -->
      <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
        <!-- Vitals Snapshot -->
        <div class="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)] shadow-xs flex flex-col justify-between">
          <div class="flex items-center justify-between border-b border-[var(--color-border)] pb-2 mb-3">
            <span class="text-xs font-bold uppercase text-[var(--color-text-secondary)] flex items-center gap-2">
              <i class="pi pi-heart-fill text-rose-500"></i> Latest Patient Vitals
            </span>
            <span *ngIf="p.latestVitals" class="text-xs text-slate-400">
              {{ p.latestVitals.recordedAtUtc | date:'shortDate' }}
            </span>
          </div>
          <div *ngIf="p.latestVitals as v; else noVitals" class="grid grid-cols-2 gap-3 text-sm">
            <div>
              <p class="text-xs text-slate-500">Blood Pressure</p>
              <p class="font-bold text-slate-800 text-base">{{ v.bloodPressure || 'N/A' }}</p>
            </div>
            <div>
              <p class="text-xs text-slate-500">Heart Rate</p>
              <p class="font-bold text-slate-800 text-base">{{ v.heartRateBpm ? v.heartRateBpm + ' bpm' : 'N/A' }}</p>
            </div>
            <div>
              <p class="text-xs text-slate-500">Body Temp</p>
              <p class="font-bold text-slate-800 text-base">{{ v.temperatureCelsius ? v.temperatureCelsius + ' °C' : 'N/A' }}</p>
            </div>
            <div>
              <p class="text-xs text-slate-500">Body Weight</p>
              <p class="font-bold text-slate-800 text-base">{{ v.weightKg ? v.weightKg + ' kg' : 'N/A' }}</p>
            </div>
          </div>
          <ng-template #noVitals>
            <p class="text-xs text-slate-400 italic py-2">No recorded vital signs available.</p>
          </ng-template>
        </div>

        <!-- Active Medications Summary -->
        <div class="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)] shadow-xs flex flex-col justify-between">
          <div class="flex items-center justify-between border-b border-[var(--color-border)] pb-2 mb-3">
            <span class="text-xs font-bold uppercase text-[var(--color-text-secondary)] flex items-center gap-2">
              <i class="pi pi-briefcase text-emerald-500"></i> Active Medications ({{ p.activeMedications.length }})
            </span>
          </div>
          <div *ngIf="p.activeMedications.length > 0; else noMeds" class="flex flex-col gap-2 max-h-32 overflow-y-auto">
            <div *ngFor="let med of p.activeMedications.slice(0, 3)" class="text-xs p-2 bg-slate-50 border border-slate-200 rounded flex justify-between items-center">
              <div>
                <span class="font-bold text-slate-800">{{ med.medicationName }}</span>
                <span class="text-slate-500"> ({{ med.dosage }})</span>
              </div>
              <span class="px-1.5 py-0.5 bg-emerald-100 text-emerald-700 font-semibold rounded text-[10px]">
                {{ med.status }}
              </span>
            </div>
          </div>
          <ng-template #noMeds>
            <p class="text-xs text-slate-400 italic py-2">No active prescriptions linked.</p>
          </ng-template>
        </div>

        <!-- Pending Diagnostics -->
        <div class="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)] shadow-xs flex flex-col justify-between">
          <div class="flex items-center justify-between border-b border-[var(--color-border)] pb-2 mb-3">
            <span class="text-xs font-bold uppercase text-[var(--color-text-secondary)] flex items-center gap-2">
              <i class="pi pi-compass text-amber-500"></i> Pending Lab Orders ({{ p.pendingLabResults.length }})
            </span>
          </div>
          <div *ngIf="p.pendingLabResults.length > 0; else noLabs" class="flex flex-col gap-2 max-h-32 overflow-y-auto">
            <div *ngFor="let lab of p.pendingLabResults.slice(0, 3)" class="text-xs p-2 bg-slate-50 border border-slate-200 rounded flex justify-between items-center">
              <div>
                <span class="font-bold text-slate-800">{{ lab.testName }}</span>
                <span class="text-slate-500"> ({{ lab.priority }})</span>
              </div>
              <span class="px-1.5 py-0.5 bg-amber-100 text-amber-700 font-semibold rounded text-[10px]">
                {{ lab.status }}
              </span>
            </div>
          </div>
          <ng-template #noLabs>
            <p class="text-xs text-slate-400 italic py-2">No pending laboratory orders.</p>
          </ng-template>
        </div>
      </div>

      <!-- EHR Timeline Feed Card -->
      <div class="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] shadow-xs p-6 flex flex-col gap-6">
        <!-- Timeline Controls & Category Filters -->
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-[var(--color-border)] pb-4">
          <h2 class="text-lg font-bold text-[var(--color-text-primary)] flex items-center gap-2">
            <i class="pi pi-history text-indigo-600"></i>
            Chronological Health History Timeline ({{ filteredTimeline().length }})
          </h2>
          <div class="flex flex-wrap items-center gap-1.5">
            <button
              pButton
              label="All Events"
              [class]="eventTypeFilter() === null ? 'p-button-primary p-button-sm' : 'p-button-outlined p-button-secondary p-button-sm'"
              (click)="setEventTypeFilter(null)"
            ></button>
            <button
              pButton
              label="Clinical Encounters"
              [class]="eventTypeFilter() === 'ClinicalNote' ? 'p-button-info p-button-sm' : 'p-button-outlined p-button-secondary p-button-sm'"
              (click)="setEventTypeFilter('ClinicalNote')"
            ></button>
            <button
              pButton
              label="Prescriptions"
              [class]="eventTypeFilter() === 'Prescription' ? 'p-button-warning p-button-sm' : 'p-button-outlined p-button-secondary p-button-sm'"
              (click)="setEventTypeFilter('Prescription')"
            ></button>
            <button
              pButton
              label="Diagnostics"
              [class]="eventTypeFilter() === 'LabOrder' ? 'p-button-help p-button-sm' : 'p-button-outlined p-button-secondary p-button-sm'"
              (click)="setEventTypeFilter('LabOrder')"
            ></button>
            <button
              pButton
              label="Billing"
              [class]="eventTypeFilter() === 'Invoice' ? 'p-button-success p-button-sm' : 'p-button-outlined p-button-secondary p-button-sm'"
              (click)="setEventTypeFilter('Invoice')"
            ></button>
          </div>
        </div>

        <!-- Visual Timeline Items Stream -->
        <div *ngIf="filteredTimeline().length > 0; else emptyTimeline" class="relative border-l-2 border-slate-200 ml-4 pl-6 flex flex-col gap-6 my-2">
          <div *ngFor="let ev of filteredTimeline()" class="relative group">
            <!-- Timeline Node Marker Icon -->
            <div
              class="absolute -left-[35px] top-0 w-8 h-8 rounded-full border-2 border-white shadow-xs flex items-center justify-center text-white text-xs font-bold"
              [ngClass]="{
                'bg-indigo-600': ev.eventType === 'ClinicalNote',
                'bg-emerald-600': ev.eventType === 'Prescription',
                'bg-purple-600': ev.eventType === 'LabOrder',
                'bg-amber-600': ev.eventType === 'Invoice',
                'bg-blue-600': ev.eventType === 'Appointment'
              }"
            >
              <i [class]="'pi ' + ev.icon"></i>
            </div>

            <!-- Card Content Box -->
            <div class="bg-slate-50 border border-slate-200 hover:border-indigo-300 rounded-lg p-4 transition-all shadow-2xs">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-slate-200/60 pb-2 mb-2">
                <div class="flex items-center gap-2">
                  <span class="font-bold text-slate-900 text-sm">{{ ev.title }}</span>
                  <p-tag [severity]="ev.badgeSeverity" [value]="ev.status"></p-tag>
                </div>
                <span class="text-xs font-medium text-slate-500">
                  {{ ev.timestampUtc | date:'medium' }}
                </span>
              </div>
              <p class="text-xs text-slate-700 leading-relaxed">{{ ev.description }}</p>
            </div>
          </div>
        </div>

        <ng-template #emptyTimeline>
          <div class="text-center py-12 text-slate-400">
            <i class="pi pi-info-circle text-3xl mb-2"></i>
            <p>No health history records match selected filter criteria.</p>
          </div>
        </ng-template>
      </div>

      <!-- Modal: Add Clinical Encounter Note -->
      <p-dialog
        header="Record Clinical Encounter & Vitals"
        [(visible)]="displayAddNoteDialog"
        [modal]="true"
        [style]="{ width: '520px' }"
        styleClass="p-fluid"
      >
        <form [formGroup]="addNoteForm" (ngSubmit)="saveClinicalNote()" class="flex flex-col gap-4 mt-2">
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Attending Physician / Doctor *</label>
            <input pInputText formControlName="doctorName" placeholder="e.g. Dr. Sarah Jenkins" />
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Chief Complaint *</label>
            <input pInputText formControlName="chiefComplaint" placeholder="e.g. Persistent fever and cough for 3 days" />
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Clinical Diagnosis *</label>
            <input pInputText formControlName="diagnosis" placeholder="e.g. Acute Upper Respiratory Tract Infection" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Blood Pressure</label>
              <input pInputText formControlName="bloodPressure" placeholder="120/80" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Heart Rate (bpm)</label>
              <input pInputText type="number" formControlName="heartRateBpm" placeholder="72" />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Body Temp (°C)</label>
              <input pInputText type="number" step="0.1" formControlName="temperatureCelsius" placeholder="37.2" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Weight (kg)</label>
              <input pInputText type="number" step="0.5" formControlName="weightKg" placeholder="70.5" />
            </div>
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Clinical Progress Notes & Treatment Plan</label>
            <input pInputText formControlName="notes" placeholder="Prescribed oral antibiotics, rest for 5 days" />
          </div>

          <div class="flex justify-end gap-2 mt-4">
            <button
              pButton
              type="button"
              label="Cancel"
              class="p-button-text p-button-secondary"
              (click)="displayAddNoteDialog = false"
            ></button>
            <button
              pButton
              type="submit"
              label="Save Note to Timeline"
              class="p-button-primary"
              [disabled]="addNoteForm.invalid || submitting()"
            ></button>
          </div>
        </form>
      </p-dialog>
    </div>

    <ng-template #loadingState>
      <div class="flex items-center justify-center py-20 text-slate-500 gap-3">
        <i class="pi pi-spin pi-spinner text-2xl"></i>
        <span>Loading patient medical record timeline...</span>
      </div>
    </ng-template>
  `
})
export class PatientEhrDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private ehrService = inject(EhrService);
  private fb = inject(FormBuilder);

  ehr = signal<PatientEhr | null>(null);
  loading = signal<boolean>(false);
  submitting = signal<boolean>(false);

  eventTypeFilter = signal<string | null>(null);

  displayAddNoteDialog = false;

  addNoteForm = this.fb.group({
    doctorName: ['Sarah Jenkins', Validators.required],
    chiefComplaint: ['', Validators.required],
    diagnosis: ['', Validators.required],
    notes: [''],
    bloodPressure: ['120/80'],
    heartRateBpm: [72],
    temperatureCelsius: [37.0],
    weightKg: [70.0]
  });

  filteredTimeline = computed(() => {
    const data = this.ehr();
    if (!data) return [];

    const filter = this.eventTypeFilter();
    if (!filter) return data.timeline;

    return data.timeline.filter(t => t.eventType.toLowerCase() === filter.toLowerCase());
  });

  ngOnInit(): void {
    const patientId = this.route.snapshot.paramMap.get('id');
    if (patientId) {
      this.loadEhr(patientId);
    }
  }

  loadEhr(patientId: string): void {
    this.loading.set(true);
    this.ehrService.getPatientEhr(patientId).subscribe({
      next: (data) => {
        this.ehr.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  setEventTypeFilter(type: string | null): void {
    this.eventTypeFilter.set(type);
  }

  openAddNoteDialog(): void {
    this.addNoteForm.reset({
      doctorName: 'Sarah Jenkins',
      chiefComplaint: '',
      diagnosis: '',
      notes: '',
      bloodPressure: '120/80',
      heartRateBpm: 72,
      temperatureCelsius: 37.0,
      weightKg: 70.0
    });
    this.displayAddNoteDialog = true;
  }

  saveClinicalNote(): void {
    const patient = this.ehr();
    if (!patient || this.addNoteForm.invalid) return;

    this.submitting.set(true);
    const val = this.addNoteForm.value;

    const req = {
      doctorName: val.doctorName!,
      chiefComplaint: val.chiefComplaint!,
      diagnosis: val.diagnosis!,
      notes: val.notes || '',
      bloodPressure: val.bloodPressure || undefined,
      heartRateBpm: val.heartRateBpm || undefined,
      temperatureCelsius: val.temperatureCelsius || undefined,
      weightKg: val.weightKg || undefined
    };

    this.ehrService.addClinicalNote(patient.patientId, req).subscribe({
      next: () => {
        this.displayAddNoteDialog = false;
        this.submitting.set(false);
        Swal.fire({
          title: 'Successful!',
          text: 'Clinical note added to EHR successfully.',
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
        // Refresh timeline
        this.loadEhr(patient.patientId);
      },
      error: () => this.submitting.set(false)
    });
  }
}
