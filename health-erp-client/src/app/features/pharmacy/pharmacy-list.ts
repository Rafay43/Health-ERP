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
import { TabsModule } from 'primeng/tabs';

import { PharmacyService } from './data-access/pharmacy.service';
import { Drug, Prescription } from './data-access/pharmacy.model';
import { PatientService } from '../patients/data-access/patient.service';
import { Patient } from '../patients/data-access/patient.model';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-pharmacy-list',
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
      <!-- Page Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Pharmacy & Medication Management</h1>
          <p class="text-sm text-[var(--color-text-secondary)]">Monitor drug inventory catalog, automated low-stock alerts, and patient prescription fulfillment</p>
        </div>
        <div class="flex items-center gap-2">
          <button
            pButton
            icon="pi pi-plus"
            label="Add Medication"
            class="p-button-primary"
            (click)="openAddDrugDialog()"
          ></button>
          <button
            pButton
            icon="pi pi-file-edit"
            label="New Prescription"
            class="p-button-outlined p-button-secondary"
            (click)="openAddPrescriptionDialog()"
          ></button>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-blue-100 text-blue-600 rounded-lg">
            <i class="pi pi-box text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Total SKUs</p>
            <h3 class="text-xl font-bold text-[var(--color-text-primary)]">{{ totalSkus() }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-amber-100 text-amber-600 rounded-lg">
            <i class="pi pi-exclamation-triangle text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Low Stock Alerts</p>
            <h3 class="text-xl font-bold text-amber-600">{{ lowStockCount() }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-rose-100 text-rose-600 rounded-lg">
            <i class="pi pi-clock text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Expiring Soon</p>
            <h3 class="text-xl font-bold text-rose-600">{{ expiringCount() }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-indigo-100 text-indigo-600 rounded-lg">
            <i class="pi pi-shield text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Pending Prescriptions</p>
            <h3 class="text-xl font-bold text-indigo-600">{{ pendingPrescriptionsCount() }}</h3>
          </div>
        </div>
      </div>

      <!-- Main Navigation Tabs -->
      <div class="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] shadow-xs overflow-hidden">
        <div class="flex border-b border-[var(--color-border)] bg-[var(--color-surface-subtle)]">
          <button
            class="px-6 py-3 font-semibold text-sm transition-colors border-b-2 flex items-center gap-2"
            [class.border-indigo-600]="activeTab() === 'inventory'"
            [class.text-indigo-600]="activeTab() === 'inventory'"
            [class.border-transparent]="activeTab() !== 'inventory'"
            [class.text-[var(--color-text-secondary)]]="activeTab() !== 'inventory'"
            (click)="activeTab.set('inventory')"
          >
            <i class="pi pi-list"></i>
            Inventory Catalog ({{ drugs().length }})
          </button>
          <button
            class="px-6 py-3 font-semibold text-sm transition-colors border-b-2 flex items-center gap-2"
            [class.border-indigo-600]="activeTab() === 'prescriptions'"
            [class.text-indigo-600]="activeTab() === 'prescriptions'"
            [class.border-transparent]="activeTab() !== 'prescriptions'"
            [class.text-[var(--color-text-secondary)]]="activeTab() !== 'prescriptions'"
            (click)="activeTab.set('prescriptions')"
          >
            <i class="pi pi-send"></i>
            Prescription Orders ({{ prescriptions().length }})
          </button>
        </div>

        <!-- Inventory Tab Content -->
        <div *ngIf="activeTab() === 'inventory'" class="p-4 flex flex-col gap-4">
          <div class="flex flex-col sm:flex-row items-center justify-between gap-4">
            <div class="relative flex items-center w-full sm:w-80">
              <i class="pi pi-search absolute left-3 text-slate-400 z-10 pointer-events-none"></i>
              <input
                type="text"
                pInputText
                placeholder="Search medication name or category..."
                class="w-full !pl-10"
                [value]="searchTerm()"
                (input)="onSearchInput($event)"
              />
            </div>
            <div class="flex items-center gap-3">
              <button
                pButton
                [icon]="lowStockFilter() ? 'pi pi-check-circle' : 'pi pi-filter'"
                [label]="'Low Stock (' + lowStockCount() + ')'"
                [class]="lowStockFilter() ? 'p-button-warning' : 'p-button-outlined p-button-secondary'"
                (click)="toggleLowStockFilter()"
              ></button>
              <button
                pButton
                [icon]="expiringFilter() ? 'pi pi-check-circle' : 'pi pi-filter'"
                [label]="'Expiring Soon (' + expiringCount() + ')'"
                [class]="expiringFilter() ? 'p-button-danger' : 'p-button-outlined p-button-secondary'"
                (click)="toggleExpiringFilter()"
              ></button>
            </div>
          </div>

          <p-table
            [value]="filteredDrugs()"
            [loading]="loading()"
            [paginator]="true"
            [rows]="10"
            [tableStyle]="{ 'min-width': '50rem' }"
            styleClass="p-datatable-sm p-datatable-gridlines"
          >
            <ng-template pTemplate="header">
              <tr>
                <th>Medication Name</th>
                <th>Category</th>
                <th>Stock Level</th>
                <th>Reorder Level</th>
                <th>Expiry Date</th>
                <th>Unit Price</th>
                <th>Status</th>
                <th class="text-center">Action</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-drug>
              <tr>
                <td>
                  <div class="font-bold text-[var(--color-text-primary)]">{{ drug.name }}</div>
                  <div class="text-xs text-[var(--color-text-secondary)]">{{ drug.genericName }}</div>
                </td>
                <td>
                  <span class="px-2 py-1 bg-slate-100 text-slate-700 font-medium rounded text-xs">
                    {{ drug.category }}
                  </span>
                </td>
                <td>
                  <span
                    class="font-bold px-2 py-0.5 rounded text-sm"
                    [ngClass]="{
                      'bg-red-100 text-red-700': drug.isLowStock,
                      'bg-emerald-100 text-emerald-700': !drug.isLowStock
                    }"
                  >
                    {{ drug.stockQuantity }} units
                  </span>
                </td>
                <td class="text-slate-600">{{ drug.reorderLevel }} units</td>
                <td>
                  <span [class.text-rose-600]="drug.isExpiringSoon" [class.font-semibold]="drug.isExpiringSoon">
                    {{ drug.expiryDateUtc | date:'mediumDate' }}
                  </span>
                </td>
                <td class="font-semibold text-slate-800">$ {{ drug.unitPrice | number:'1.2-2' }}</td>
                <td>
                  <div class="flex flex-wrap gap-1">
                    <p-tag *ngIf="drug.isLowStock" severity="warn" value="Low Stock"></p-tag>
                    <p-tag *ngIf="drug.isExpiringSoon" severity="danger" value="Expiring Soon"></p-tag>
                    <p-tag *ngIf="!drug.isLowStock && !drug.isExpiringSoon" severity="success" value="Optimal"></p-tag>
                  </div>
                </td>
                <td class="text-center">
                  <button
                    pButton
                    icon="pi pi-sync"
                    label="Adjust Stock"
                    class="p-button-text p-button-sm p-button-secondary"
                    (click)="openAdjustStockDialog(drug)"
                  ></button>
                </td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr>
                <td colspan="8" class="text-center py-8 text-slate-500">
                  No medication inventory records found.
                </td>
              </tr>
            </ng-template>
          </p-table>
        </div>

        <!-- Prescriptions Tab Content -->
        <div *ngIf="activeTab() === 'prescriptions'" class="p-4 flex flex-col gap-4">
          <div class="flex items-center justify-between gap-4">
            <h3 class="text-lg font-semibold text-[var(--color-text-primary)]">Patient Prescriptions & Dispensing</h3>
            <div class="flex items-center gap-2">
              <button
                pButton
                label="All"
                [class]="prescriptionStatusFilter() === null ? 'p-button-primary' : 'p-button-outlined p-button-secondary'"
                (click)="setPrescriptionStatusFilter(null)"
              ></button>
              <button
                pButton
                label="Pending"
                [class]="prescriptionStatusFilter() === 'Pending' ? 'p-button-warning' : 'p-button-outlined p-button-secondary'"
                (click)="setPrescriptionStatusFilter('Pending')"
              ></button>
              <button
                pButton
                label="Dispensed"
                [class]="prescriptionStatusFilter() === 'Dispensed' ? 'p-button-success' : 'p-button-outlined p-button-secondary'"
                (click)="setPrescriptionStatusFilter('Dispensed')"
              ></button>
            </div>
          </div>

          <p-table
            [value]="filteredPrescriptions()"
            [loading]="loading()"
            [paginator]="true"
            [rows]="10"
            [tableStyle]="{ 'min-width': '50rem' }"
            styleClass="p-datatable-sm p-datatable-gridlines"
          >
            <ng-template pTemplate="header">
              <tr>
                <th>Patient</th>
                <th>Medication & Dosage</th>
                <th>Prescribing Doctor</th>
                <th>Frequency / Duration</th>
                <th>Prescribed Date</th>
                <th>Status</th>
                <th class="text-center">Fulfillment Action</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-rx>
              <tr>
                <td class="font-bold text-[var(--color-text-primary)]">
                  {{ rx.patientName }}
                </td>
                <td>
                  <div class="font-semibold text-slate-900">{{ rx.medicationName }}</div>
                  <div class="text-xs text-indigo-600 font-medium">Dosage: {{ rx.dosage }}</div>
                </td>
                <td class="text-slate-700">Dr. {{ rx.doctorName }}</td>
                <td class="text-xs text-slate-600">
                  <div>{{ rx.frequency }}</div>
                  <div>{{ rx.durationDays }} days ({{ rx.refills }} refills)</div>
                </td>
                <td>{{ rx.prescribedDateUtc | date:'shortDate' }}</td>
                <td>
                  <p-tag
                    [severity]="getRxSeverity(rx.status)"
                    [value]="rx.status"
                  ></p-tag>
                </td>
                <td class="text-center">
                  <button
                    *ngIf="rx.status === 'Pending'"
                    pButton
                    icon="pi pi-check"
                    label="Dispense & Deduct"
                    class="p-button-success p-button-sm"
                    (click)="dispensePrescription(rx)"
                  ></button>
                  <span *ngIf="rx.status === 'Dispensed'" class="text-xs text-slate-500 italic">
                    Dispensed on {{ rx.dispensedDateUtc | date:'short' }}
                  </span>
                </td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr>
                <td colspan="7" class="text-center py-8 text-slate-500">
                  No prescriptions found matching criteria.
                </td>
              </tr>
            </ng-template>
          </p-table>
        </div>
      </div>

      <!-- Modal: Add New Medication -->
      <p-dialog
        header="Add New Medication SKU"
        [(visible)]="displayAddDrugDialog"
        [modal]="true"
        [style]="{ width: '500px' }"
        styleClass="p-fluid"
      >
        <form [formGroup]="addDrugForm" (ngSubmit)="saveDrug()" class="flex flex-col gap-4 mt-2">
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Brand / Commercial Name *</label>
            <input pInputText formControlName="name" placeholder="e.g. Amoxicillin 500mg" />
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Generic Name</label>
            <input pInputText formControlName="genericName" placeholder="e.g. Amoxicillin" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Category *</label>
              <input pInputText formControlName="category" placeholder="e.g. Antibiotics" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Unit Price ($) *</label>
              <input pInputText type="number" step="0.01" formControlName="unitPrice" />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Initial Stock Quantity *</label>
              <input pInputText type="number" formControlName="stockQuantity" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Reorder Threshold *</label>
              <input pInputText type="number" formControlName="reorderLevel" />
            </div>
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Expiry Date *</label>
            <input pInputText type="date" formControlName="expiryDate" />
          </div>

          <div class="flex justify-end gap-2 mt-4">
            <button
              pButton
              type="button"
              label="Cancel"
              class="p-button-text p-button-secondary"
              (click)="displayAddDrugDialog = false"
            ></button>
            <button
              pButton
              type="submit"
              label="Save Medication"
              class="p-button-primary"
              [disabled]="addDrugForm.invalid || submitting()"
            ></button>
          </div>
        </form>
      </p-dialog>

      <!-- Modal: Adjust Stock -->
      <p-dialog
        header="Update Inventory Stock"
        [(visible)]="displayAdjustStockDialog"
        [modal]="true"
        [style]="{ width: '400px' }"
        styleClass="p-fluid"
      >
        <div *ngIf="selectedDrugForStock" class="flex flex-col gap-4 mt-2">
          <div class="p-3 bg-slate-50 border border-slate-200 rounded-lg">
            <div class="font-bold text-slate-800">{{ selectedDrugForStock.name }}</div>
            <div class="text-xs text-slate-600">Current Stock: <span class="font-bold">{{ selectedDrugForStock.stockQuantity }} units</span></div>
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">New Quantity Count *</label>
            <input pInputText type="number" [(ngModel)]="newStockValue" placeholder="Enter updated quantity" />
          </div>

          <div class="flex justify-end gap-2 mt-2">
            <button
              pButton
              type="button"
              label="Cancel"
              class="p-button-text p-button-secondary"
              (click)="displayAdjustStockDialog = false"
            ></button>
            <button
              pButton
              type="button"
              label="Update Stock"
              class="p-button-primary"
              (click)="saveAdjustedStock()"
              [disabled]="newStockValue < 0 || submitting()"
            ></button>
          </div>
        </div>
      </p-dialog>

      <!-- Modal: Create Prescription -->
      <p-dialog
        header="Issue New Prescription"
        [(visible)]="displayAddPrescriptionDialog"
        [modal]="true"
        [style]="{ width: '520px' }"
        styleClass="p-fluid"
      >
        <form [formGroup]="addPrescriptionForm" (ngSubmit)="savePrescription()" class="flex flex-col gap-4 mt-2">
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Select Patient *</label>
            <select
              formControlName="patientId"
              class="w-full p-2 border border-slate-300 rounded-md bg-white text-sm"
            >
              <option value="" disabled>-- Select Registered Patient --</option>
              <option *ngFor="let p of patientList()" [value]="p.id">
                {{ p.firstName }} {{ p.lastName }} (MRN: {{ p.medicalRecordNumber }})
              </option>
            </select>
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Select Medication (Optional SKU link)</label>
            <select
              formControlName="drugId"
              class="w-full p-2 border border-slate-300 rounded-md bg-white text-sm"
              (change)="onDrugSelect($event)"
            >
              <option value="">-- Custom Medication (Unlinked) --</option>
              <option *ngFor="let drug of drugs()" [value]="drug.id">
                {{ drug.name }} (Stock: {{ drug.stockQuantity }})
              </option>
            </select>
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Medication Name *</label>
            <input pInputText formControlName="medicationName" placeholder="e.g. Amoxicillin 500mg" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Prescribing Doctor *</label>
              <input pInputText formControlName="doctorName" placeholder="e.g. Sarah Jenkins" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Dosage *</label>
              <input pInputText formControlName="dosage" placeholder="e.g. 500mg" />
            </div>
          </div>

          <div class="grid grid-cols-3 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Frequency *</label>
              <input pInputText formControlName="frequency" placeholder="e.g. Twice daily" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Duration (Days)</label>
              <input pInputText type="number" formControlName="durationDays" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Refills</label>
              <input pInputText type="number" formControlName="refills" />
            </div>
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Instructions / Notes</label>
            <input pInputText formControlName="instructions" placeholder="Take after meals with water" />
          </div>

          <div class="flex justify-end gap-2 mt-4">
            <button
              pButton
              type="button"
              label="Cancel"
              class="p-button-text p-button-secondary"
              (click)="displayAddPrescriptionDialog = false"
            ></button>
            <button
              pButton
              type="submit"
              label="Issue Order"
              class="p-button-primary"
              [disabled]="addPrescriptionForm.invalid || submitting()"
            ></button>
          </div>
        </form>
      </p-dialog>
    </div>
  `
})
export class PharmacyListComponent implements OnInit {
  private pharmacyService = inject(PharmacyService);
  private patientService = inject(PatientService);
  private fb = inject(FormBuilder);

  // State Signals
  drugs = signal<Drug[]>([]);
  prescriptions = signal<Prescription[]>([]);
  patientList = signal<Patient[]>([]);
  loading = signal<boolean>(false);
  submitting = signal<boolean>(false);

  activeTab = signal<'inventory' | 'prescriptions'>('inventory');

  searchTerm = signal<string>('');
  lowStockFilter = signal<boolean>(false);
  expiringFilter = signal<boolean>(false);
  prescriptionStatusFilter = signal<string | null>(null);

  // Dialog Visibility Flags
  displayAddDrugDialog = false;
  displayAdjustStockDialog = false;
  displayAddPrescriptionDialog = false;

  selectedDrugForStock: Drug | null = null;
  newStockValue = 0;

  // Forms
  addDrugForm = this.fb.group({
    name: ['', Validators.required],
    genericName: [''],
    category: ['General', Validators.required],
    stockQuantity: [100, [Validators.required, Validators.min(0)]],
    reorderLevel: [20, [Validators.required, Validators.min(0)]],
    expiryDate: [new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString().substring(0, 10), Validators.required],
    unitPrice: [10.0, [Validators.required, Validators.min(0)]]
  });

  addPrescriptionForm = this.fb.group({
    patientId: ['', Validators.required],
    drugId: [''],
    doctorName: ['Sarah Jenkins', Validators.required],
    medicationName: ['', Validators.required],
    dosage: ['500mg', Validators.required],
    frequency: ['Twice daily', Validators.required],
    durationDays: [7, Validators.min(1)],
    refills: [0, Validators.min(0)],
    instructions: ['Take after meals']
  });

  // Computed signals
  totalSkus = computed(() => this.drugs().length);
  lowStockCount = computed(() => this.drugs().filter(d => d.isLowStock).length);
  expiringCount = computed(() => this.drugs().filter(d => d.isExpiringSoon).length);
  pendingPrescriptionsCount = computed(() => this.prescriptions().filter(p => p.status === 'Pending').length);

  filteredDrugs = computed(() => {
    let result = this.drugs();
    const search = this.searchTerm().trim().toLowerCase();

    if (search) {
      result = result.filter(d =>
        d.name.toLowerCase().includes(search) ||
        d.genericName.toLowerCase().includes(search) ||
        d.category.toLowerCase().includes(search)
      );
    }

    if (this.lowStockFilter()) {
      result = result.filter(d => d.isLowStock);
    }

    if (this.expiringFilter()) {
      result = result.filter(d => d.isExpiringSoon);
    }

    return result;
  });

  filteredPrescriptions = computed(() => {
    let result = this.prescriptions();
    const status = this.prescriptionStatusFilter();

    if (status) {
      result = result.filter(p => p.status.toLowerCase() === status.toLowerCase());
    }

    return result;
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.pharmacyService.getInventory().subscribe({
      next: (inventory) => {
        this.drugs.set(inventory);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });

    this.pharmacyService.getPrescriptions().subscribe({
      next: (rxs) => {
        this.prescriptions.set(rxs);
      }
    });

    this.patientService.getPatients().subscribe({
      next: (pts) => {
        this.patientList.set(pts);
      }
    });
  }

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchTerm.set(value);
  }

  toggleLowStockFilter(): void {
    this.lowStockFilter.set(!this.lowStockFilter());
  }

  toggleExpiringFilter(): void {
    this.expiringFilter.set(!this.expiringFilter());
  }

  setPrescriptionStatusFilter(status: string | null): void {
    this.prescriptionStatusFilter.set(status);
  }

  openAddDrugDialog(): void {
    this.addDrugForm.reset({
      name: '',
      genericName: '',
      category: 'General',
      stockQuantity: 100,
      reorderLevel: 20,
      expiryDate: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString().substring(0, 10),
      unitPrice: 10.0
    });
    this.displayAddDrugDialog = true;
  }

  saveDrug(): void {
    if (this.addDrugForm.invalid) return;

    this.submitting.set(true);
    const val = this.addDrugForm.value;
    const req = {
      name: val.name!,
      genericName: val.genericName || '',
      category: val.category!,
      stockQuantity: val.stockQuantity!,
      reorderLevel: val.reorderLevel!,
      expiryDateUtc: new Date(val.expiryDate!).toISOString(),
      unitPrice: val.unitPrice!
    };

    this.pharmacyService.createDrug(req).subscribe({
      next: (newDrug) => {
        this.drugs.update(list => [newDrug, ...list]);
        this.displayAddDrugDialog = false;
        this.submitting.set(false);
        Swal.fire({
          title: 'Successful!',
          text: `Drug ${newDrug.name} added to inventory.`,
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      },
      error: () => this.submitting.set(false)
    });
  }

  openAdjustStockDialog(drug: Drug): void {
    this.selectedDrugForStock = drug;
    this.newStockValue = drug.stockQuantity;
    this.displayAdjustStockDialog = true;
  }

  saveAdjustedStock(): void {
    if (!this.selectedDrugForStock) return;

    this.submitting.set(true);
    this.pharmacyService.updateStock(this.selectedDrugForStock.id, this.newStockValue).subscribe({
      next: (updated) => {
        this.drugs.update(list => list.map(d => d.id === updated.id ? updated : d));
        this.displayAdjustStockDialog = false;
        this.submitting.set(false);
        Swal.fire({
          title: 'Successful!',
          text: `Stock for ${updated.name} updated to ${updated.stockQuantity} units.`,
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      },
      error: () => this.submitting.set(false)
    });
  }

  openAddPrescriptionDialog(): void {
    this.addPrescriptionForm.reset({
      patientId: this.patientList().length > 0 ? this.patientList()[0].id : '',
      drugId: '',
      doctorName: 'Sarah Jenkins',
      medicationName: '',
      dosage: '500mg',
      frequency: 'Twice daily',
      durationDays: 7,
      refills: 0,
      instructions: 'Take after meals'
    });
    this.displayAddPrescriptionDialog = true;
  }

  onDrugSelect(event: Event): void {
    const drugId = (event.target as HTMLSelectElement).value;
    if (drugId) {
      const selected = this.drugs().find(d => d.id === drugId);
      if (selected) {
        this.addPrescriptionForm.patchValue({
          medicationName: selected.name
        });
      }
    }
  }

  savePrescription(): void {
    if (this.addPrescriptionForm.invalid) return;

    this.submitting.set(true);
    const val = this.addPrescriptionForm.value;

    const req = {
      patientId: val.patientId!,
      drugId: val.drugId || undefined,
      doctorName: val.doctorName!,
      medicationName: val.medicationName!,
      dosage: val.dosage!,
      frequency: val.frequency!,
      durationDays: val.durationDays || 7,
      refills: val.refills || 0,
      instructions: val.instructions || ''
    };

    this.pharmacyService.createPrescription(req).subscribe({
      next: (newRx) => {
        this.prescriptions.update(list => [newRx, ...list]);
        this.displayAddPrescriptionDialog = false;
        this.submitting.set(false);
        Swal.fire({
          title: 'Successful!',
          text: `Prescription for ${newRx.medicationName} created successfully.`,
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      },
      error: () => this.submitting.set(false)
    });
  }

  dispensePrescription(rx: Prescription): void {
    this.pharmacyService.dispensePrescription(rx.id).subscribe({
      next: (dispensedRx) => {
        this.prescriptions.update(list => list.map(p => p.id === dispensedRx.id ? dispensedRx : p));
        // Refresh inventory to reflect deducted stock
        this.loadData();
        Swal.fire({
          title: 'Successful!',
          text: `Prescription for ${dispensedRx.medicationName} dispensed successfully.`,
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      }
    });
  }

  getRxSeverity(status: string): 'warn' | 'success' | 'danger' | 'info' {
    switch (status.toLowerCase()) {
      case 'dispensed': return 'success';
      case 'cancelled': return 'danger';
      default: return 'warn';
    }
  }
}
