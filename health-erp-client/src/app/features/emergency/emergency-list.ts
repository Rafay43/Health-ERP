import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';

import { EmergencyService } from './data-access/emergency.service';
import { TriageRecord } from './data-access/emergency.model';
import { PatientService } from '../patients/data-access/patient.service';
import { Patient } from '../patients/data-access/patient.model';
import { InpatientService } from '../inpatient/data-access/inpatient.service';
import { Bed } from '../inpatient/data-access/inpatient.model';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-emergency-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    TableModule,
    ButtonModule,
    TagModule,
    TooltipModule,
    InputTextModule,
    DialogModule,
    MessageModule,
  ],
  template: `
    <div class="flex flex-col gap-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Emergency Department (ED) & Triage</h1>
          <p class="text-sm text-[var(--color-text-secondary)]">
            ESI Triage Level 1–5 triage queue, vital signs monitoring, bay allocation, and swift ward bed transfer
          </p>
        </div>
        <div class="flex items-center gap-2">
          <button
            pButton
            icon="pi pi-plus"
            label="Rapid ER Intake / Triage"
            class="p-button-danger"
            (click)="openAddTriageModal()"
          ></button>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-rose-50 p-4 rounded-xl border border-rose-200 shadow-xs flex items-center gap-4">
          <div class="p-3 bg-rose-600 text-white rounded-lg animate-pulse">
            <i class="pi pi-heart-fill text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-semibold text-rose-800 uppercase">Level 1 - Resuscitation</p>
            <h3 class="text-2xl font-black text-rose-900">{{ level1Count() }}</h3>
          </div>
        </div>

        <div class="bg-amber-50 p-4 rounded-xl border border-amber-200 shadow-xs flex items-center gap-4">
          <div class="p-3 bg-amber-500 text-white rounded-lg">
            <i class="pi pi-bolt text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-semibold text-amber-800 uppercase">Level 2 - Emergent</p>
            <h3 class="text-2xl font-black text-amber-900">{{ level2Count() }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-blue-100 text-blue-600 rounded-lg">
            <i class="pi pi-users text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">In Bay Care</p>
            <h3 class="text-xl font-bold text-[var(--color-text-primary)]">{{ inBayCareCount() }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-purple-100 text-purple-600 rounded-lg">
            <i class="pi pi-clock text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Total Active ER Queue</p>
            <h3 class="text-xl font-bold text-purple-600">{{ activeQueueCount() }}</h3>
          </div>
        </div>
      </div>

      <!-- Filter Bar -->
      <div class="flex items-center justify-between border-b border-[var(--color-border)] pb-3">
        <div class="flex items-center gap-2">
          <button
            class="px-4 py-1.5 font-medium text-xs rounded-lg transition-colors border"
            [ngClass]="activeOnlyFilter() ? 'bg-rose-600 text-white border-rose-600' : 'bg-white text-slate-700 border-slate-300'"
            (click)="toggleActiveFilter(true)"
          >
            Active ER Queue
          </button>
          <button
            class="px-4 py-1.5 font-medium text-xs rounded-lg transition-colors border"
            [ngClass]="!activeOnlyFilter() ? 'bg-rose-600 text-white border-rose-600' : 'bg-white text-slate-700 border-slate-300'"
            (click)="toggleActiveFilter(false)"
          >
            All Historical Triage Logs
          </button>
        </div>
      </div>

      <!-- MAIN ER TRIAGE QUEUE TABLE -->
      <div class="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] shadow-xs overflow-hidden">
        <p-table
          [value]="triageRecords()"
          [rows]="10"
          [paginator]="true"
          styleClass="p-datatable-sm"
        >
          <ng-template pTemplate="header">
            <tr>
              <th>ESI Triage Severity</th>
              <th>Patient</th>
              <th>Chief Complaint</th>
              <th>Vital Signs</th>
              <th>Assigned Bay</th>
              <th>Status</th>
              <th>Arrival Time</th>
              <th class="text-right">ER Actions</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-rec>
            <tr [ngClass]="{'bg-rose-50/40': rec.severityLevel === 1, 'bg-amber-50/30': rec.severityLevel === 2}">
              <td>
                <span
                  class="px-2.5 py-1 rounded-full text-xs font-bold inline-flex items-center gap-1 shadow-2xs"
                  [ngClass]="getSeverityBadgeClass(rec.severityLevel)"
                >
                  <span class="w-2 h-2 rounded-full" [ngClass]="getSeverityDotClass(rec.severityLevel)"></span>
                  Level {{ rec.severityLevel }} - {{ getSeverityLabel(rec.severityLevel) }}
                </span>
              </td>
              <td>
                <div class="font-bold text-slate-800">{{ rec.patientName }}</div>
                <div class="text-xs text-slate-500">MRN: {{ rec.patientMrn }}</div>
              </td>
              <td class="max-w-xs truncate font-medium text-slate-700" [pTooltip]="rec.chiefComplaint">
                {{ rec.chiefComplaint }}
              </td>
              <td>
                <div class="text-xs flex flex-wrap gap-1">
                  <span class="px-1.5 py-0.5 bg-slate-100 rounded text-slate-700 font-mono">BP: {{ rec.bloodPressure }}</span>
                  <span class="px-1.5 py-0.5 rounded font-mono" [ngClass]="rec.heartRateBpm > 100 ? 'bg-amber-100 text-amber-800 font-bold' : 'bg-slate-100 text-slate-700'">
                    HR: {{ rec.heartRateBpm }}
                  </span>
                  <span class="px-1.5 py-0.5 rounded font-mono" [ngClass]="rec.oxygenSaturationSpO2 < 95 ? 'bg-rose-100 text-rose-800 font-bold' : 'bg-slate-100 text-slate-700'">
                    SpO2: {{ rec.oxygenSaturationSpO2 }}%
                  </span>
                  <span class="px-1.5 py-0.5 bg-slate-100 rounded text-slate-700 font-mono">T: {{ rec.bodyTemperatureCelsius }}°C</span>
                </div>
              </td>
              <td>
                <div *ngIf="rec.assignedBay" class="font-bold text-indigo-700 text-sm">
                  <i class="pi pi-map-marker text-xs mr-1"></i>{{ rec.assignedBay }}
                </div>
                <div *ngIf="!rec.assignedBay" class="text-xs text-amber-600 italic">
                  Unassigned (Waiting)
                </div>
                <div *ngIf="rec.attendingPhysicianName" class="text-xs text-slate-500">
                  {{ rec.attendingPhysicianName }}
                </div>
              </td>
              <td>
                <p-tag
                  [value]="getStatusLabel(rec.statusName)"
                  [severity]="getStatusSeverity(rec.statusName)"
                ></p-tag>
              </td>
              <td>{{ rec.arrivalDateUtc | date:'shortTime' }}</td>
              <td class="text-right">
                <div *ngIf="rec.statusName !== 'DischargedFromED' && rec.statusName !== 'TransferredToInpatient'" class="flex items-center justify-end gap-1">
                  <button
                    pButton
                    icon="pi pi-map-marker"
                    pTooltip="Assign Bay / Doctor"
                    class="p-button-xs p-button-outlined p-button-secondary"
                    (click)="openAssignBayModal(rec)"
                  ></button>
                  <button
                    pButton
                    icon="pi pi-building"
                    pTooltip="Transfer to Ward Bed"
                    class="p-button-xs p-button-primary"
                    (click)="openTransferModal(rec)"
                  ></button>
                  <button
                    pButton
                    icon="pi pi-check"
                    pTooltip="Discharge from ER"
                    class="p-button-xs p-button-success p-button-text"
                    (click)="dischargeFromER(rec)"
                  ></button>
                </div>
                <span *ngIf="rec.statusName === 'DischargedFromED' || rec.statusName === 'TransferredToInpatient'" class="text-xs text-slate-400">
                  Resolved
                </span>
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="8" class="text-center py-8 text-slate-500">
                No active patients in the Emergency Triage queue.
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>

      <!-- MODAL 1: RAPID ER TRIAGE INTAKE -->
      <p-dialog
        [(visible)]="showTriageDialog"
        header="Emergency Department Intake & Triage"
        [modal]="true"
        [style]="{ width: '520px' }"
      >
        <form [formGroup]="triageForm" (ngSubmit)="submitTriage()" class="flex flex-col gap-4 mt-2">
          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Emergency Patient</label>
            <select
              formControlName="patientId"
              class="w-full px-3 py-2 rounded-lg border border-slate-300 bg-white text-sm focus:ring-2 focus:ring-rose-500"
            >
              <option value="" disabled>-- Select Patient --</option>
              <option *ngFor="let p of patientList()" [value]="p.id">
                {{ p.firstName }} {{ p.lastName }} (MRN: {{ p.medicalRecordNumber }})
              </option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">ESI Triage Severity Level</label>
            <select
              formControlName="severity"
              class="w-full px-3 py-2 rounded-lg border-2 font-bold text-sm focus:ring-2"
              [ngClass]="getSeveritySelectClass(triageForm.value.severity)"
            >
              <option [value]="1">Level 1 - Resuscitation (Immediate Life Threat)</option>
              <option [value]="2">Level 2 - Emergent (High Risk / Urgent)</option>
              <option [value]="3">Level 3 - Urgent (Multiple Resources Needed)</option>
              <option [value]="4">Level 4 - Less Urgent (One Resource Needed)</option>
              <option [value]="5">Level 5 - Non-Urgent (No Resources Needed)</option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Chief Complaint</label>
            <textarea
              rows="2"
              pInputText
              formControlName="chiefComplaint"
              placeholder="e.g. Severe chest pain, respiratory distress, trauma..."
              class="w-full"
            ></textarea>
          </div>

          <!-- Vitals Grid -->
          <div class="p-3 bg-slate-50 rounded-xl border border-slate-200">
            <span class="text-xs font-bold text-slate-700 uppercase block mb-2">Initial Vital Signs</span>
            <div class="grid grid-cols-3 gap-3">
              <div>
                <label class="block text-2xs font-medium text-slate-500">Blood Pressure</label>
                <input pInputText formControlName="bloodPressure" placeholder="120/80" class="w-full text-xs" />
              </div>
              <div>
                <label class="block text-2xs font-medium text-slate-500">Heart Rate (BPM)</label>
                <input pInputText type="number" formControlName="heartRateBpm" placeholder="80" class="w-full text-xs" />
              </div>
              <div>
                <label class="block text-2xs font-medium text-slate-500">SpO2 (%)</label>
                <input pInputText type="number" formControlName="oxygenSaturationSpO2" placeholder="98" class="w-full text-xs" />
              </div>
              <div>
                <label class="block text-2xs font-medium text-slate-500">Temp (°C)</label>
                <input pInputText type="number" step="0.1" formControlName="bodyTemperatureCelsius" placeholder="37.0" class="w-full text-xs" />
              </div>
              <div>
                <label class="block text-2xs font-medium text-slate-500">Resp Rate (BPM)</label>
                <input pInputText type="number" formControlName="respiratoryRateBpm" placeholder="16" class="w-full text-xs" />
              </div>
              <div>
                <label class="block text-2xs font-medium text-slate-500">Assign Bay (Opt.)</label>
                <input pInputText formControlName="assignedBay" placeholder="Trauma Bay 1" class="w-full text-xs" />
              </div>
            </div>
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Triage Nurse Name</label>
            <input pInputText formControlName="triageNurseName" class="w-full" />
          </div>

          <div class="flex justify-end gap-2 mt-2">
            <button pButton type="button" label="Cancel" class="p-button-text" (click)="showTriageDialog.set(false)"></button>
            <button pButton type="submit" label="Register ER Triage" class="p-button-danger" [disabled]="triageForm.invalid"></button>
          </div>
        </form>
      </p-dialog>

      <!-- MODAL 2: ASSIGN BAY & PHYSICIAN -->
      <p-dialog
        [(visible)]="showAssignBayDialog"
        header="Assign ER Bay & Attending Physician"
        [modal]="true"
        [style]="{ width: '420px' }"
      >
        <form [formGroup]="assignBayForm" (ngSubmit)="submitAssignBay()" class="flex flex-col gap-4 mt-2">
          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">ER Bay / Bed Name</label>
            <input pInputText formControlName="bayName" placeholder="e.g. Trauma Bay 01, ER Bed 3" class="w-full" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Attending Physician</label>
            <input pInputText formControlName="attendingPhysicianName" placeholder="Dr. Gregory House" class="w-full" />
          </div>

          <div class="flex justify-end gap-2 mt-2">
            <button pButton type="button" label="Cancel" class="p-button-text" (click)="showAssignBayDialog.set(false)"></button>
            <button pButton type="submit" label="Save Bay Assignment" class="p-button-primary" [disabled]="assignBayForm.invalid"></button>
          </div>
        </form>
      </p-dialog>

      <!-- MODAL 3: TRANSFER TO INPATIENT WARD -->
      <p-dialog
        [(visible)]="showTransferDialog"
        header="Transfer ER Patient to Inpatient Ward"
        [modal]="true"
        [style]="{ width: '480px' }"
      >
        <div *ngIf="selectedTriageForTransfer()" class="flex flex-col gap-4 mt-2">
          <div class="p-3 bg-slate-50 rounded-lg border border-slate-200 text-xs">
            <p class="font-bold text-slate-800 text-sm mb-1">{{ selectedTriageForTransfer()?.patientName }}</p>
            <p class="text-slate-600">Chief Complaint: {{ selectedTriageForTransfer()?.chiefComplaint }}</p>
          </div>

          <form [formGroup]="transferForm" (ngSubmit)="submitTransfer()" class="flex flex-col gap-4">
            <div>
              <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Select Available Ward Bed</label>
              <select
                formControlName="bedId"
                class="w-full px-3 py-2 rounded-lg border border-slate-300 bg-white text-sm focus:ring-2 focus:ring-indigo-500"
              >
                <option value="" disabled>-- Select Inpatient Bed --</option>
                <option *ngFor="let b of availableBeds()" [value]="b.id">
                  {{ b.wardName }} - Room {{ b.roomNumber }} ({{ b.bedNumber }}) - $ {{ b.dailyRate }}/day
                </option>
              </select>
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Admitting Physician</label>
              <input pInputText formControlName="admittingDoctorName" placeholder="Dr. Sarah Connor" class="w-full" />
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Admission Transfer Notes</label>
              <textarea
                rows="3"
                pInputText
                formControlName="admissionNotes"
                placeholder="Urgent transfer from ER for continuous monitoring and surgical intervention..."
                class="w-full"
              ></textarea>
            </div>

            <div class="flex justify-end gap-2 mt-2">
              <button pButton type="button" label="Cancel" class="p-button-text" (click)="showTransferDialog.set(false)"></button>
              <button pButton type="submit" label="Execute Ward Transfer" class="p-button-success" [disabled]="transferForm.invalid"></button>
            </div>
          </form>
        </div>
      </p-dialog>
    </div>
  `
})
export class EmergencyListComponent implements OnInit {
  private emergencyService = inject(EmergencyService);
  private patientService = inject(PatientService);
  private inpatientService = inject(InpatientService);
  private fb = inject(FormBuilder);

  // State Signals
  triageRecords = signal<TriageRecord[]>([]);
  patientList = signal<Patient[]>([]);
  availableBeds = signal<Bed[]>([]);

  activeOnlyFilter = signal<boolean>(true);

  showTriageDialog = signal<boolean>(false);
  showAssignBayDialog = signal<boolean>(false);
  showTransferDialog = signal<boolean>(false);

  selectedTriageForBay = signal<TriageRecord | null>(null);
  selectedTriageForTransfer = signal<TriageRecord | null>(null);

  // Reactive Forms
  triageForm = this.fb.group({
    patientId: ['', Validators.required],
    severity: [2, Validators.required],
    chiefComplaint: ['', Validators.required],
    bloodPressure: ['120/80', Validators.required],
    heartRateBpm: [88, [Validators.required, Validators.min(20)]],
    oxygenSaturationSpO2: [96, [Validators.required, Validators.min(50), Validators.max(100)]],
    bodyTemperatureCelsius: [37.0, Validators.required],
    respiratoryRateBpm: [18, Validators.required],
    triageNurseName: ['Nurse Clara Barton', Validators.required],
    assignedBay: ['']
  });

  assignBayForm = this.fb.group({
    bayName: ['', Validators.required],
    attendingPhysicianName: ['Dr. Gregory House', Validators.required]
  });

  transferForm = this.fb.group({
    bedId: ['', Validators.required],
    admittingDoctorName: ['Dr. Sarah Connor', Validators.required],
    admissionNotes: ['Direct ER transfer for inpatient care', Validators.required]
  });

  // Computed Signals
  level1Count = computed(() => this.triageRecords().filter(r => r.severityLevel === 1 && r.statusName !== 'DischargedFromED' && r.statusName !== 'TransferredToInpatient').length);
  level2Count = computed(() => this.triageRecords().filter(r => r.severityLevel === 2 && r.statusName !== 'DischargedFromED' && r.statusName !== 'TransferredToInpatient').length);
  inBayCareCount = computed(() => this.triageRecords().filter(r => r.statusName === 'InBayCare').length);
  activeQueueCount = computed(() => this.triageRecords().filter(r => r.statusName !== 'DischargedFromED' && r.statusName !== 'TransferredToInpatient').length);

  ngOnInit() {
    this.loadEmergencyQueue();
    this.loadPatients();
    this.loadBeds();
  }

  loadEmergencyQueue() {
    this.emergencyService.getEmergencyQueue(this.activeOnlyFilter()).subscribe({
      next: (data) => this.triageRecords.set(data),
      error: (err) => console.error('Failed to load emergency queue', err)
    });
  }

  loadPatients() {
    this.patientService.getPatients().subscribe({
      next: (data) => this.patientList.set(data),
      error: (err) => console.error('Failed to load patients', err)
    });
  }

  loadBeds() {
    this.inpatientService.getBedGrid().subscribe({
      next: (data) => this.availableBeds.set(data.filter(b => b.status === 'Available')),
      error: (err) => console.error('Failed to load beds', err)
    });
  }

  toggleActiveFilter(active: boolean) {
    this.activeOnlyFilter.set(active);
    this.loadEmergencyQueue();
  }

  getSeverityBadgeClass(level: number): string {
    switch (level) {
      case 1: return 'bg-rose-600 text-white animate-pulse';
      case 2: return 'bg-amber-500 text-white';
      case 3: return 'bg-yellow-400 text-slate-900';
      case 4: return 'bg-emerald-500 text-white';
      case 5: return 'bg-blue-500 text-white';
      default: return 'bg-slate-500 text-white';
    }
  }

  getSeverityDotClass(level: number): string {
    switch (level) {
      case 1: return 'bg-white animate-ping';
      case 2: return 'bg-white';
      default: return 'bg-slate-200';
    }
  }

  getSeveritySelectClass(level: any): string {
    const lvl = Number(level);
    switch (lvl) {
      case 1: return 'border-rose-600 bg-rose-50 text-rose-900';
      case 2: return 'border-amber-500 bg-amber-50 text-amber-900';
      case 3: return 'border-yellow-500 bg-yellow-50 text-yellow-900';
      default: return 'border-slate-300';
    }
  }

  getSeverityLabel(level: number): string {
    switch (level) {
      case 1: return 'Resuscitation';
      case 2: return 'Emergent';
      case 3: return 'Urgent';
      case 4: return 'Less Urgent';
      case 5: return 'Non-Urgent';
      default: return 'Unknown';
    }
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case 'WaitingForTriage': return 'Waiting Triage';
      case 'Triaged': return 'Triaged';
      case 'InBayCare': return 'In Bay Care';
      case 'TransferredToInpatient': return 'Transferred to Ward';
      case 'DischargedFromED': return 'Discharged ER';
      default: return status;
    }
  }

  getStatusSeverity(status: string): 'success' | 'danger' | 'warn' | 'info' | 'secondary' {
    switch (status) {
      case 'InBayCare': return 'info';
      case 'Triaged': return 'warn';
      case 'TransferredToInpatient': return 'success';
      case 'DischargedFromED': return 'secondary';
      default: return 'danger';
    }
  }

  openAddTriageModal() {
    this.triageForm.reset({
      patientId: '',
      severity: 2,
      chiefComplaint: '',
      bloodPressure: '120/80',
      heartRateBpm: 88,
      oxygenSaturationSpO2: 96,
      bodyTemperatureCelsius: 37.0,
      respiratoryRateBpm: 18,
      triageNurseName: 'Nurse Clara Barton',
      assignedBay: ''
    });
    this.showTriageDialog.set(true);
  }

  submitTriage() {
    if (this.triageForm.invalid) return;

    const val = this.triageForm.value;
    this.emergencyService.createTriageRecord({
      patientId: val.patientId!,
      chiefComplaint: val.chiefComplaint!,
      bloodPressure: val.bloodPressure!,
      heartRateBpm: Number(val.heartRateBpm),
      oxygenSaturationSpO2: Number(val.oxygenSaturationSpO2),
      bodyTemperatureCelsius: Number(val.bodyTemperatureCelsius),
      respiratoryRateBpm: Number(val.respiratoryRateBpm),
      severity: Number(val.severity),
      triageNurseName: val.triageNurseName!,
      assignedBay: val.assignedBay || undefined
    }).subscribe({
      next: () => {
        this.showTriageDialog.set(false);
        this.loadEmergencyQueue();
        Swal.fire({
          title: 'Successful!',
          text: 'Emergency triage record saved successfully.',
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      },
      error: (err) => {
        Swal.fire({
          title: 'Error',
          text: err.error?.detail || err.error?.message || 'Failed to create ER triage.',
          icon: 'error',
          confirmButtonColor: '#ef4444'
        });
      }
    });
  }

  openAssignBayModal(rec: TriageRecord) {
    this.selectedTriageForBay.set(rec);
    this.assignBayForm.reset({
      bayName: rec.assignedBay || 'Trauma Bay 01',
      attendingPhysicianName: rec.attendingPhysicianName || 'Dr. Gregory House'
    });
    this.showAssignBayDialog.set(true);
  }

  submitAssignBay() {
    const rec = this.selectedTriageForBay();
    if (!rec || this.assignBayForm.invalid) return;

    const val = this.assignBayForm.value;
    this.emergencyService.assignBay(rec.id, {
      bayName: val.bayName!,
      attendingPhysicianName: val.attendingPhysicianName!
    }).subscribe({
      next: () => {
        this.showAssignBayDialog.set(false);
        this.selectedTriageForBay.set(null);
        this.loadEmergencyQueue();
        Swal.fire({
          title: 'Successful!',
          text: `Assigned to ${val.bayName} successfully.`,
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      },
      error: (err) => {
        Swal.fire({
          title: 'Error',
          text: err.error?.detail || err.error?.message || 'Failed to assign bay.',
          icon: 'error',
          confirmButtonColor: '#ef4444'
        });
      }
    });
  }

  openTransferModal(rec: TriageRecord) {
    this.selectedTriageForTransfer.set(rec);
    this.transferForm.reset({
      bedId: '',
      admittingDoctorName: rec.attendingPhysicianName || 'Dr. Sarah Connor',
      admissionNotes: `ER Transfer: ${rec.chiefComplaint}`
    });
    this.showTransferDialog.set(true);
  }

  submitTransfer() {
    const rec = this.selectedTriageForTransfer();
    if (!rec || this.transferForm.invalid) return;

    const val = this.transferForm.value;
    this.emergencyService.transferToInpatient(rec.id, {
      bedId: val.bedId!,
      admittingDoctorName: val.admittingDoctorName!,
      admissionNotes: val.admissionNotes!
    }).subscribe({
      next: () => {
        this.showTransferDialog.set(false);
        this.selectedTriageForTransfer.set(null);
        this.loadEmergencyQueue();
        this.loadBeds();
        Swal.fire({
          title: 'Successful!',
          text: 'Patient transferred to inpatient ward successfully.',
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      },
      error: (err) => {
        Swal.fire({
          title: 'Error',
          text: err.error?.detail || err.error?.message || 'Failed to transfer patient to ward.',
          icon: 'error',
          confirmButtonColor: '#ef4444'
        });
      }
    });
  }

  dischargeFromER(rec: TriageRecord) {
    Swal.fire({
      title: 'Discharge Patient?',
      text: `Discharge ${rec.patientName} from Emergency Department?`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonColor: '#ef4444',
      cancelButtonColor: '#6b7280',
      confirmButtonText: 'Yes, Discharge'
    }).then((result) => {
      if (result.isConfirmed) {
        this.emergencyService.dischargeFromEmergency(rec.id).subscribe({
          next: () => {
            this.loadEmergencyQueue();
            Swal.fire({
              title: 'Successful!',
              text: 'Patient discharged from Emergency Department.',
              icon: 'success',
              confirmButtonColor: '#10b981',
              timer: 2500,
              timerProgressBar: true
            });
          },
          error: (err) => {
            Swal.fire({
              title: 'Error',
              text: err.error?.detail || err.error?.message || 'Failed to discharge patient from ER.',
              icon: 'error',
              confirmButtonColor: '#ef4444'
            });
          }
        });
      }
    });
  }
}
