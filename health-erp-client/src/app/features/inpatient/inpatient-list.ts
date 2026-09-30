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

import { InpatientService } from './data-access/inpatient.service';
import { Bed, Admission } from './data-access/inpatient.model';
import { PatientService } from '../patients/data-access/patient.service';
import { Patient } from '../patients/data-access/patient.model';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-inpatient-list',
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
          <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Inpatient Ward & Bed Management</h1>
          <p class="text-sm text-[var(--color-text-secondary)]">
            Real-time ward occupancy monitoring, bed allocation grid, and patient admission/discharge tracking
          </p>
        </div>
        <div class="flex items-center gap-2">
          <button
            pButton
            icon="pi pi-plus"
            label="Add Hospital Bed"
            class="p-button-outlined p-button-secondary"
            (click)="openAddBedModal()"
          ></button>
          <button
            pButton
            icon="pi pi-sign-in"
            label="Admit Patient"
            class="p-button-primary"
            (click)="openAdmitModal()"
          ></button>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-blue-100 text-blue-600 rounded-lg">
            <i class="pi pi-building text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Total Beds</p>
            <h3 class="text-xl font-bold text-[var(--color-text-primary)]">{{ totalBedsCount() }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-rose-100 text-rose-600 rounded-lg">
            <i class="pi pi-[#ef4444] text-xl pi-user"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Occupied Beds</p>
            <h3 class="text-xl font-bold text-rose-600">
              {{ occupiedBedsCount() }} <span class="text-xs font-normal text-slate-500">({{ occupancyRate() }}%)</span>
            </h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-emerald-100 text-emerald-600 rounded-lg">
            <i class="pi pi-check-circle text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Available Beds</p>
            <h3 class="text-xl font-bold text-emerald-600">{{ availableBedsCount() }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-purple-100 text-purple-600 rounded-lg">
            <i class="pi pi-users text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Active Inpatients</p>
            <h3 class="text-xl font-bold text-purple-600">{{ activeAdmissionsCount() }}</h3>
          </div>
        </div>
      </div>

      <!-- Navigation Tabs & Ward Filter -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between border-b border-[var(--color-border)] gap-4 pb-2">
        <div class="flex gap-2">
          <button
            class="px-4 py-2 font-medium text-sm rounded-lg transition-colors"
            [ngClass]="activeTab() === 'grid' ? 'bg-indigo-600 text-white shadow-xs' : 'text-slate-600 hover:bg-slate-100'"
            (click)="setActiveTab('grid')"
          >
            <i class="pi pi-th-large mr-1.5"></i> Bed Allocation Grid
          </button>
          <button
            class="px-4 py-2 font-medium text-sm rounded-lg transition-colors"
            [ngClass]="activeTab() === 'admissions' ? 'bg-indigo-600 text-white shadow-xs' : 'text-slate-600 hover:bg-slate-100'"
            (click)="setActiveTab('admissions')"
          >
            <i class="pi pi-list mr-1.5"></i> Admission Records
          </button>
        </div>

        <div *ngIf="activeTab() === 'grid'" class="flex items-center gap-2">
          <span class="text-sm font-medium text-slate-600">Ward Filter:</span>
          <select
            class="px-3 py-1.5 text-sm rounded-lg border border-slate-300 bg-white shadow-xs focus:ring-2 focus:ring-indigo-500"
            [ngModel]="selectedWard()"
            (ngModelChange)="onWardFilterChange($event)"
          >
            <option value="">All Wards ({{ totalBedsCount() }})</option>
            <option *ngFor="let ward of wardList()" [value]="ward">{{ ward }}</option>
          </select>
        </div>
      </div>

      <!-- TAB 1: BED ALLOCATION GRID -->
      <div *ngIf="activeTab() === 'grid'" class="flex flex-col gap-6">
        <div *ngIf="filteredBeds().length === 0" class="p-8 text-center bg-slate-50 rounded-xl border border-dashed border-slate-300">
          <i class="pi pi-inbox text-4xl text-slate-400 mb-2"></i>
          <p class="text-slate-600 font-medium">No beds found for the selected ward.</p>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          <div
            *ngFor="let bed of filteredBeds()"
            class="bg-[var(--color-surface)] rounded-xl border transition-all duration-200 shadow-xs hover:shadow-md overflow-hidden flex flex-col justify-between"
            [ngClass]="{
              'border-emerald-200 bg-emerald-50/20': bed.status === 'Available',
              'border-rose-200 bg-rose-50/20': bed.status === 'Occupied',
              'border-amber-200 bg-amber-50/20': bed.status === 'Maintenance',
              'border-slate-200 bg-slate-50/20': bed.status === 'Cleaning'
            }"
          >
            <!-- Card Header -->
            <div class="p-4 border-b flex items-start justify-between"
                 [ngClass]="{
                   'border-emerald-100 bg-emerald-100/30': bed.status === 'Available',
                   'border-rose-100 bg-rose-100/30': bed.status === 'Occupied',
                   'border-amber-100 bg-amber-100/30': bed.status === 'Maintenance',
                   'border-slate-100 bg-slate-100/30': bed.status === 'Cleaning'
                 }"
            >
              <div>
                <span class="text-xs font-semibold uppercase tracking-wider text-slate-500">{{ bed.wardName }}</span>
                <h3 class="text-lg font-bold text-slate-800">
                  Room {{ bed.roomNumber }} - {{ bed.bedNumber }}
                </h3>
                <p class="text-xs text-slate-500">{{ bed.bedType }}</p>
              </div>

              <p-tag
                [value]="bed.status"
                [severity]="getBedStatusSeverity(bed.status)"
              ></p-tag>
            </div>

            <!-- Card Body -->
            <div class="p-4 flex-1 flex flex-col justify-between gap-3">
              <div>
                <p class="text-xs text-slate-500 mb-1">Daily Rate</p>
                <p class="text-base font-bold text-slate-700">$ {{ bed.dailyRate | number:'1.2-2' }} / day</p>
              </div>

              <!-- Occupant Details if Occupied -->
              <div *ngIf="bed.status === 'Occupied'" class="p-3 bg-white rounded-lg border border-rose-200 flex flex-col gap-1 shadow-2xs">
                <span class="text-xs font-semibold text-rose-700 flex items-center gap-1">
                  <i class="pi pi-user text-xs"></i> Current Inpatient
                </span>
                <p class="text-sm font-bold text-slate-800">{{ bed.occupiedByPatientName || 'Patient' }}</p>
                <p class="text-xs text-slate-500">MRN: {{ bed.occupiedByPatientMrn || 'N/A' }}</p>
              </div>

              <div *ngIf="bed.status === 'Available'" class="p-3 bg-emerald-50/50 rounded-lg border border-emerald-100 text-xs text-emerald-800 flex items-center gap-1.5">
                <i class="pi pi-check text-emerald-600"></i> Ready for patient admission
              </div>
            </div>

            <!-- Card Actions Footer -->
            <div class="p-3 bg-slate-50/80 border-t border-slate-100 flex items-center justify-end gap-2">
              <button
                *ngIf="bed.status === 'Available'"
                pButton
                icon="pi pi-sign-in"
                label="Admit Here"
                class="p-button-sm p-button-success w-full"
                (click)="openAdmitModalForBed(bed)"
              ></button>

              <button
                *ngIf="bed.status === 'Occupied'"
                pButton
                icon="pi pi-sign-out"
                label="Discharge"
                class="p-button-sm p-button-danger p-button-outlined w-full"
                (click)="openDischargeModalForBed(bed)"
              ></button>
            </div>
          </div>
        </div>
      </div>

      <!-- TAB 2: ADMISSION RECORDS -->
      <div *ngIf="activeTab() === 'admissions'" class="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] shadow-xs overflow-hidden">
        <div class="p-4 border-b border-[var(--color-border)] flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <h2 class="text-lg font-bold text-[var(--color-text-primary)]">Inpatient Admission Log</h2>
          <div class="flex items-center gap-2">
            <button
              class="px-3 py-1.5 text-xs font-medium rounded-lg border transition-colors"
              [ngClass]="activeOnlyFilter() ? 'bg-indigo-600 text-white border-indigo-600' : 'bg-white text-slate-700 border-slate-300'"
              (click)="toggleActiveOnlyFilter()"
            >
              Active Stays Only
            </button>
          </div>
        </div>

        <p-table
          [value]="admissions()"
          [rows]="10"
          [paginator]="true"
          styleClass="p-datatable-sm"
        >
          <ng-template pTemplate="header">
            <tr>
              <th>Patient</th>
              <th>Ward & Bed</th>
              <th>Admitting Doctor</th>
              <th>Chief Complaint</th>
              <th>Admission Date</th>
              <th>Discharge Date</th>
              <th>Room Charge</th>
              <th>Status</th>
              <th class="text-right">Action</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-adm>
            <tr>
              <td>
                <div class="font-semibold text-slate-800">{{ adm.patientName }}</div>
                <div class="text-xs text-slate-500">MRN: {{ adm.patientMrn }}</div>
              </td>
              <td>
                <div class="font-medium text-slate-700">{{ adm.wardName }}</div>
                <div class="text-xs text-slate-500">Room {{ adm.roomNumber }} - {{ adm.bedNumber }}</div>
              </td>
              <td>{{ adm.admittingDoctorName }}</td>
              <td class="max-w-xs truncate" [pTooltip]="adm.chiefComplaint">{{ adm.chiefComplaint }}</td>
              <td>{{ adm.admissionDateUtc | date:'short' }}</td>
              <td>
                <span *ngIf="adm.dischargeDateUtc">{{ adm.dischargeDateUtc | date:'short' }}</span>
                <span *ngIf="!adm.dischargeDateUtc" class="text-slate-400 italic">In Care</span>
              </td>
              <td>
                <div class="font-bold text-slate-800">
                  <span *ngIf="adm.isDischarged">$ {{ adm.totalRoomCharge | number:'1.2-2' }}</span>
                  <span *ngIf="!adm.isDischarged" class="text-slate-500 font-normal">$ {{ adm.dailyRateSnapshot | number:'1.2-2' }}/day</span>
                </div>
              </td>
              <td>
                <p-tag
                  [value]="adm.isDischarged ? 'Discharged' : 'Active Stay'"
                  [severity]="adm.isDischarged ? 'contrast' : 'warn'"
                ></p-tag>
              </td>
              <td class="text-right">
                <button
                  *ngIf="!adm.isDischarged"
                  pButton
                  icon="pi pi-sign-out"
                  label="Discharge"
                  class="p-button-sm p-button-danger p-button-text"
                  (click)="openDischargeModalForAdmission(adm)"
                ></button>
                <span *ngIf="adm.isDischarged" class="text-xs text-slate-400">Completed</span>
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="9" class="text-center py-8 text-slate-500">
                No inpatient admissions recorded yet.
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>

      <!-- MODAL 1: ADD HOSPITAL BED -->
      <p-dialog
        [(visible)]="showAddBedDialog"
        header="Add New Hospital Bed"
        [modal]="true"
        [style]="{ width: '450px' }"
      >
        <form [formGroup]="bedForm" (ngSubmit)="submitAddBed()" class="flex flex-col gap-4 mt-2">
          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Ward Name</label>
            <input pInputText formControlName="wardName" placeholder="e.g. ICU Ward, General Ward A" class="w-full" />
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Room Number</label>
              <input pInputText formControlName="roomNumber" placeholder="e.g. 201" class="w-full" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Bed Number</label>
              <input pInputText formControlName="bedNumber" placeholder="e.g. Bed-01" class="w-full" />
            </div>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Bed Type</label>
              <input pInputText formControlName="bedType" placeholder="e.g. Standard, ICU, VIP" class="w-full" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Daily Rate ($)</label>
              <input pInputText type="number" formControlName="dailyRate" placeholder="100.00" class="w-full" />
            </div>
          </div>

          <div class="flex justify-end gap-2 mt-4">
            <button pButton type="button" label="Cancel" class="p-button-text" (click)="showAddBedDialog.set(false)"></button>
            <button pButton type="submit" label="Create Bed" class="p-button-primary" [disabled]="bedForm.invalid"></button>
          </div>
        </form>
      </p-dialog>

      <!-- MODAL 2: ADMIT PATIENT -->
      <p-dialog
        [(visible)]="showAdmitDialog"
        header="Admit Inpatient to Hospital"
        [modal]="true"
        [style]="{ width: '500px' }"
      >
        <form [formGroup]="admitForm" (ngSubmit)="submitAdmitPatient()" class="flex flex-col gap-4 mt-2">
          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Select Patient</label>
            <select
              formControlName="patientId"
              class="w-full px-3 py-2 rounded-lg border border-slate-300 bg-white text-sm focus:ring-2 focus:ring-indigo-500"
            >
              <option value="" disabled>-- Choose Patient --</option>
              <option *ngFor="let p of availablePatients()" [value]="p.id">
                {{ p.firstName }} {{ p.lastName }} (MRN: {{ p.medicalRecordNumber }})
              </option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Assigned Bed</label>
            <select
              formControlName="bedId"
              class="w-full px-3 py-2 rounded-lg border border-slate-300 bg-white text-sm focus:ring-2 focus:ring-indigo-500"
            >
              <option value="" disabled>-- Select Available Bed --</option>
              <option *ngFor="let b of availableBedsList()" [value]="b.id">
                {{ b.wardName }} - Room {{ b.roomNumber }} ({{ b.bedNumber }}) - $ {{ b.dailyRate }}/day
              </option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Admitting Physician / Doctor</label>
            <input pInputText formControlName="admittingDoctorName" placeholder="Dr. Jane Smith" class="w-full" />
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Chief Complaint / Admission Reason</label>
            <textarea
              rows="3"
              pInputText
              formControlName="chiefComplaint"
              placeholder="Primary diagnosis, symptoms, or surgical recovery notes..."
              class="w-full"
            ></textarea>
          </div>

          <div class="flex justify-end gap-2 mt-4">
            <button pButton type="button" label="Cancel" class="p-button-text" (click)="showAdmitDialog.set(false)"></button>
            <button pButton type="submit" label="Confirm Admission" class="p-button-success" [disabled]="admitForm.invalid"></button>
          </div>
        </form>
      </p-dialog>

      <!-- MODAL 3: DISCHARGE PATIENT -->
      <p-dialog
        [(visible)]="showDischargeDialog"
        header="Process Patient Discharge"
        [modal]="true"
        [style]="{ width: '480px' }"
      >
        <div *ngIf="selectedAdmissionForDischarge()" class="flex flex-col gap-4 mt-2">
          <div class="p-4 bg-slate-50 rounded-xl border border-slate-200 flex flex-col gap-2">
            <div class="flex items-center justify-between">
              <span class="text-sm font-bold text-slate-800">{{ selectedAdmissionForDischarge()?.patientName }}</span>
              <span class="text-xs text-slate-500">MRN: {{ selectedAdmissionForDischarge()?.patientMrn }}</span>
            </div>
            <div class="text-xs text-slate-600">
              Bed: {{ selectedAdmissionForDischarge()?.wardName }} - Room {{ selectedAdmissionForDischarge()?.roomNumber }} ({{ selectedAdmissionForDischarge()?.bedNumber }})
            </div>
            <div class="text-xs text-slate-600">
              Admission Date: {{ selectedAdmissionForDischarge()?.admissionDateUtc | date:'medium' }}
            </div>
            <div class="text-xs font-semibold text-indigo-700">
              Daily Room Rate Snapshot: $ {{ selectedAdmissionForDischarge()?.dailyRateSnapshot | number:'1.2-2' }} / day
            </div>
          </div>

          <form [formGroup]="dischargeForm" (ngSubmit)="submitDischargePatient()" class="flex flex-col gap-4">
            <div>
              <label class="block text-xs font-semibold text-slate-700 uppercase mb-1">Discharge Summary & Discharge Instructions</label>
              <textarea
                rows="4"
                pInputText
                formControlName="dischargeSummary"
                placeholder="Patient recovered, medications prescribed, follow-up appointment instructions..."
                class="w-full"
              ></textarea>
            </div>

            <div class="flex justify-end gap-2 mt-4">
              <button pButton type="button" label="Cancel" class="p-button-text" (click)="showDischargeDialog.set(false)"></button>
              <button pButton type="submit" label="Complete Discharge & Finalize Charge" class="p-button-danger" [disabled]="dischargeForm.invalid"></button>
            </div>
          </form>
        </div>
      </p-dialog>
    </div>
  `
})
export class InpatientListComponent implements OnInit {
  private inpatientService = inject(InpatientService);
  private patientService = inject(PatientService);
  private fb = inject(FormBuilder);

  // State Signals
  beds = signal<Bed[]>([]);
  admissions = signal<Admission[]>([]);
  availablePatients = signal<Patient[]>([]);

  activeTab = signal<'grid' | 'admissions'>('grid');
  selectedWard = signal<string>('');
  activeOnlyFilter = signal<boolean>(false);

  showAddBedDialog = signal<boolean>(false);
  showAdmitDialog = signal<boolean>(false);
  showDischargeDialog = signal<boolean>(false);

  selectedAdmissionForDischarge = signal<Admission | null>(null);

  // Reactive Forms
  bedForm = this.fb.group({
    wardName: ['', Validators.required],
    roomNumber: ['', Validators.required],
    bedNumber: ['', Validators.required],
    bedType: ['Standard', Validators.required],
    dailyRate: [150, [Validators.required, Validators.min(0)]]
  });

  admitForm = this.fb.group({
    patientId: ['', Validators.required],
    bedId: ['', Validators.required],
    admittingDoctorName: ['Dr. Sarah Connor', Validators.required],
    chiefComplaint: ['', Validators.required]
  });

  dischargeForm = this.fb.group({
    dischargeSummary: ['Patient stabilized and discharged home with follow-up instructions.', Validators.required]
  });

  // Computed Signals
  totalBedsCount = computed(() => this.beds().length);
  occupiedBedsCount = computed(() => this.beds().filter(b => b.status === 'Occupied').length);
  availableBedsCount = computed(() => this.beds().filter(b => b.status === 'Available').length);
  occupancyRate = computed(() => {
    const total = this.totalBedsCount();
    if (total === 0) return 0;
    return Math.round((this.occupiedBedsCount() / total) * 100);
  });
  activeAdmissionsCount = computed(() => this.admissions().filter(a => !a.isDischarged).length);

  wardList = computed(() => {
    const wards = new Set(this.beds().map(b => b.wardName));
    return Array.from(wards).sort();
  });

  filteredBeds = computed(() => {
    const ward = this.selectedWard();
    if (!ward) return this.beds();
    return this.beds().filter(b => b.wardName.toLowerCase() === ward.toLowerCase());
  });

  availableBedsList = computed(() => this.beds().filter(b => b.status === 'Available'));

  ngOnInit() {
    this.loadBeds();
    this.loadAdmissions();
    this.loadPatients();
  }

  loadBeds() {
    this.inpatientService.getBedGrid().subscribe({
      next: (data) => this.beds.set(data),
      error: (err) => console.error('Failed to load bed grid', err)
    });
  }

  loadAdmissions() {
    this.inpatientService.getAdmissions(undefined, this.activeOnlyFilter()).subscribe({
      next: (data) => this.admissions.set(data),
      error: (err) => console.error('Failed to load admissions', err)
    });
  }

  loadPatients() {
    this.patientService.getPatients().subscribe({
      next: (data) => this.availablePatients.set(data),
      error: (err) => console.error('Failed to load patients', err)
    });
  }

  setActiveTab(tab: 'grid' | 'admissions') {
    this.activeTab.set(tab);
  }

  onWardFilterChange(ward: string) {
    this.selectedWard.set(ward);
  }

  toggleActiveOnlyFilter() {
    this.activeOnlyFilter.update(v => !v);
    this.loadAdmissions();
  }

  getBedStatusSeverity(status: string): 'success' | 'danger' | 'warn' | 'info' | 'secondary' {
    switch (status) {
      case 'Available': return 'success';
      case 'Occupied': return 'danger';
      case 'Maintenance': return 'warn';
      default: return 'secondary';
    }
  }

  openAddBedModal() {
    this.bedForm.reset({
      wardName: 'General Ward A',
      roomNumber: '101',
      bedNumber: 'Bed-01',
      bedType: 'Standard',
      dailyRate: 150
    });
    this.showAddBedDialog.set(true);
  }

  submitAddBed() {
    if (this.bedForm.invalid) return;

    const val = this.bedForm.value;
    this.inpatientService.createBed({
      wardName: val.wardName!,
      roomNumber: val.roomNumber!,
      bedNumber: val.bedNumber!,
      bedType: val.bedType!,
      dailyRate: Number(val.dailyRate)
    }).subscribe({
      next: () => {
        this.showAddBedDialog.set(false);
        this.loadBeds();
        Swal.fire({
          title: 'Successful!',
          text: `Bed ${val.bedNumber} in ${val.wardName} created successfully.`,
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      },
      error: (err) => {
        Swal.fire({
          title: 'Error',
          text: err.error?.detail || err.error?.message || 'Failed to create bed.',
          icon: 'error',
          confirmButtonColor: '#ef4444'
        });
      }
    });
  }

  openAdmitModal() {
    this.admitForm.reset({
      patientId: '',
      bedId: '',
      admittingDoctorName: 'Dr. Sarah Connor',
      chiefComplaint: ''
    });
    this.showAdmitDialog.set(true);
  }

  openAdmitModalForBed(bed: Bed) {
    this.admitForm.reset({
      patientId: '',
      bedId: bed.id,
      admittingDoctorName: 'Dr. Sarah Connor',
      chiefComplaint: ''
    });
    this.showAdmitDialog.set(true);
  }

  submitAdmitPatient() {
    if (this.admitForm.invalid) return;

    const val = this.admitForm.value;
    this.inpatientService.admitPatient({
      patientId: val.patientId!,
      bedId: val.bedId!,
      admittingDoctorName: val.admittingDoctorName!,
      chiefComplaint: val.chiefComplaint!
    }).subscribe({
      next: () => {
        this.showAdmitDialog.set(false);
        this.loadBeds();
        this.loadAdmissions();
        Swal.fire({
          title: 'Successful!',
          text: 'Patient admitted successfully.',
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      },
      error: (err) => {
        Swal.fire({
          title: 'Error',
          text: err.error?.detail || err.error?.message || 'Failed to admit patient.',
          icon: 'error',
          confirmButtonColor: '#ef4444'
        });
      }
    });
  }

  openDischargeModalForBed(bed: Bed) {
    if (!bed.currentAdmissionId) {
      // Find from admissions list
      const adm = this.admissions().find(a => a.bedId === bed.id && !a.isDischarged);
      if (adm) {
        this.selectedAdmissionForDischarge.set(adm);
        this.showDischargeDialog.set(true);
      } else {
        Swal.fire({ title: 'Error', text: 'Could not locate active admission for this bed.', icon: 'error' });
      }
      return;
    }
    const adm = this.admissions().find(a => a.id === bed.currentAdmissionId);
    if (adm) {
      this.selectedAdmissionForDischarge.set(adm);
      this.showDischargeDialog.set(true);
    }
  }

  openDischargeModalForAdmission(adm: Admission) {
    this.selectedAdmissionForDischarge.set(adm);
    this.showDischargeDialog.set(true);
  }

  submitDischargePatient() {
    const adm = this.selectedAdmissionForDischarge();
    if (!adm || this.dischargeForm.invalid) return;

    const summary = this.dischargeForm.value.dischargeSummary!;
    this.inpatientService.dischargePatient(adm.id, { dischargeSummary: summary }).subscribe({
      next: () => {
        this.showDischargeDialog.set(false);
        this.selectedAdmissionForDischarge.set(null);
        this.loadBeds();
        this.loadAdmissions();
        Swal.fire({
          title: 'Successful!',
          text: 'Patient discharged successfully.',
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      },
      error: (err) => {
        Swal.fire({
          title: 'Error',
          text: err.error?.detail || err.error?.message || 'Failed to discharge patient.',
          icon: 'error',
          confirmButtonColor: '#ef4444'
        });
      }
    });
  }
}
