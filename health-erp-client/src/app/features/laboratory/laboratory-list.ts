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

import { LaboratoryService } from './data-access/laboratory.service';
import { LabTest, LabOrder } from './data-access/laboratory.model';

@Component({
  selector: 'app-laboratory-list',
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
          <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Laboratory & Diagnostics Management</h1>
          <p class="text-sm text-[var(--color-text-secondary)]">Manage laboratory test catalog, patient diagnostic orders, and result entries with abnormal flagging</p>
        </div>
        <div class="flex items-center gap-2">
          <button
            pButton
            icon="pi pi-plus"
            label="Add Lab Test"
            class="p-button-primary"
            (click)="openAddTestDialog()"
          ></button>
          <button
            pButton
            icon="pi pi-send"
            label="Order Test"
            class="p-button-outlined p-button-secondary"
            (click)="openAddOrderDialog()"
          ></button>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-indigo-100 text-indigo-600 rounded-lg">
            <i class="pi pi-compass text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Cataloged Tests</p>
            <h3 class="text-xl font-bold text-[var(--color-text-primary)]">{{ totalTests() }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-amber-100 text-amber-600 rounded-lg">
            <i class="pi pi-clock text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Pending Orders</p>
            <h3 class="text-xl font-bold text-amber-600">{{ pendingOrdersCount() }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-rose-100 text-rose-600 rounded-lg">
            <i class="pi pi-bolt text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">STAT / Urgent</p>
            <h3 class="text-xl font-bold text-rose-600">{{ urgentOrdersCount() }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-purple-100 text-purple-600 rounded-lg">
            <i class="pi pi-exclamation-triangle text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Abnormal Flagged</p>
            <h3 class="text-xl font-bold text-purple-600">{{ abnormalCount() }}</h3>
          </div>
        </div>
      </div>

      <!-- Main Navigation Tabs -->
      <div class="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] shadow-xs overflow-hidden">
        <div class="flex border-b border-[var(--color-border)] bg-[var(--color-surface-subtle)]">
          <button
            class="px-6 py-3 font-semibold text-sm transition-colors border-b-2 flex items-center gap-2"
            [class.border-indigo-600]="activeTab() === 'orders'"
            [class.text-indigo-600]="activeTab() === 'orders'"
            [class.border-transparent]="activeTab() !== 'orders'"
            [class.text-[var(--color-text-secondary)]]="activeTab() !== 'orders'"
            (click)="activeTab.set('orders')"
          >
            <i class="pi pi-list"></i>
            Diagnostic Orders & Results ({{ orders().length }})
          </button>
          <button
            class="px-6 py-3 font-semibold text-sm transition-colors border-b-2 flex items-center gap-2"
            [class.border-indigo-600]="activeTab() === 'catalog'"
            [class.text-indigo-600]="activeTab() === 'catalog'"
            [class.border-transparent]="activeTab() !== 'catalog'"
            [class.text-[var(--color-text-secondary)]]="activeTab() !== 'catalog'"
            (click)="activeTab.set('catalog')"
          >
            <i class="pi pi-book"></i>
            Test Catalog ({{ catalog().length }})
          </button>
        </div>

        <!-- Orders & Results Tab Content -->
        <div *ngIf="activeTab() === 'orders'" class="p-4 flex flex-col gap-4">
          <div class="flex flex-col sm:flex-row items-center justify-between gap-4">
            <div class="flex items-center gap-2">
              <button
                pButton
                label="All"
                [class]="statusFilter() === null ? 'p-button-primary' : 'p-button-outlined p-button-secondary'"
                (click)="setStatusFilter(null)"
              ></button>
              <button
                pButton
                label="Ordered / Pending"
                [class]="statusFilter() === 'Ordered' ? 'p-button-warning' : 'p-button-outlined p-button-secondary'"
                (click)="setStatusFilter('Ordered')"
              ></button>
              <button
                pButton
                label="Completed"
                [class]="statusFilter() === 'Completed' ? 'p-button-success' : 'p-button-outlined p-button-secondary'"
                (click)="setStatusFilter('Completed')"
              ></button>
            </div>
            <div class="flex items-center gap-2">
              <button
                pButton
                [icon]="abnormalOnlyFilter() ? 'pi pi-check-circle' : 'pi pi-filter'"
                [label]="'Abnormal (' + abnormalCount() + ')'"
                [class]="abnormalOnlyFilter() ? 'p-button-danger' : 'p-button-outlined p-button-secondary'"
                (click)="toggleAbnormalFilter()"
              ></button>
            </div>
          </div>

          <p-table
            [value]="filteredOrders()"
            [loading]="loading()"
            [paginator]="true"
            [rows]="10"
            [tableStyle]="{ 'min-width': '55rem' }"
            styleClass="p-datatable-sm p-datatable-gridlines"
          >
            <ng-template pTemplate="header">
              <tr>
                <th>Patient</th>
                <th>Test Name & Code</th>
                <th>Ordering Doctor</th>
                <th>Priority</th>
                <th>Status</th>
                <th>Result Details</th>
                <th class="text-center">Action</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-order>
              <tr>
                <td class="font-bold text-[var(--color-text-primary)]">
                  {{ order.patientName }}
                </td>
                <td>
                  <div class="font-semibold text-slate-900">{{ order.testName }}</div>
                  <div class="text-xs text-slate-500">Code: {{ order.testCode }}</div>
                </td>
                <td class="text-slate-700">Dr. {{ order.orderingDoctorName }}</td>
                <td>
                  <p-tag
                    [severity]="getPrioritySeverity(order.priority)"
                    [value]="order.priority"
                  ></p-tag>
                </td>
                <td>
                  <p-tag
                    [severity]="getStatusSeverity(order.status)"
                    [value]="order.status"
                  ></p-tag>
                </td>
                <td>
                  <div *ngIf="order.resultValue" class="text-xs">
                    <span class="font-bold" [class.text-rose-600]="order.isAbnormal" [class.text-slate-900]="!order.isAbnormal">
                      {{ order.resultValue }} {{ order.unit }}
                    </span>
                    <div class="text-slate-500">Ref: {{ order.referenceRange || 'N/A' }}</div>
                    <div *ngIf="order.isAbnormal" class="text-rose-600 font-bold flex items-center gap-1 mt-0.5">
                      <i class="pi pi-exclamation-circle"></i> ABNORMAL
                    </div>
                  </div>
                  <span *ngIf="!order.resultValue" class="text-xs text-slate-400 italic">Pending Entry</span>
                </td>
                <td class="text-center">
                  <button
                    *ngIf="order.status !== 'Completed' && order.status !== 'Cancelled'"
                    pButton
                    icon="pi pi-pencil"
                    label="Enter Result"
                    class="p-button-primary p-button-sm"
                    (click)="openRecordResultDialog(order)"
                  ></button>
                  <span *ngIf="order.status === 'Completed'" class="text-xs text-slate-500">
                    Done by {{ order.performedBy || 'Tech' }}
                  </span>
                </td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr>
                <td colspan="7" class="text-center py-8 text-slate-500">
                  No diagnostic orders found.
                </td>
              </tr>
            </ng-template>
          </p-table>
        </div>

        <!-- Test Catalog Tab Content -->
        <div *ngIf="activeTab() === 'catalog'" class="p-4 flex flex-col gap-4">
          <div class="flex items-center justify-between gap-4">
            <div class="relative w-full sm:w-80">
              <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"></i>
              <input
                type="text"
                pInputText
                placeholder="Search test name or code..."
                class="w-full pl-9"
                [value]="catalogSearchTerm()"
                (input)="onCatalogSearchInput($event)"
              />
            </div>
          </div>

          <p-table
            [value]="filteredCatalog()"
            [loading]="loading()"
            [paginator]="true"
            [rows]="10"
            [tableStyle]="{ 'min-width': '50rem' }"
            styleClass="p-datatable-sm p-datatable-gridlines"
          >
            <ng-template pTemplate="header">
              <tr>
                <th>Test Name</th>
                <th>Code</th>
                <th>Category</th>
                <th>Reference Range</th>
                <th>Sample Type</th>
                <th>Standard Price</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-test>
              <tr>
                <td class="font-bold text-[var(--color-text-primary)]">{{ test.name }}</td>
                <td>
                  <span class="font-mono bg-slate-100 text-slate-800 px-2 py-0.5 rounded text-xs">
                    {{ test.code }}
                  </span>
                </td>
                <td>
                  <span class="px-2 py-1 bg-indigo-50 text-indigo-700 font-medium rounded text-xs">
                    {{ test.category }}
                  </span>
                </td>
                <td class="text-slate-700">{{ test.referenceRange }} {{ test.unit }}</td>
                <td class="text-slate-600">{{ test.sampleType }}</td>
                <td class="font-semibold text-slate-900">$ {{ test.standardPrice | number:'1.2-2' }}</td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr>
                <td colspan="6" class="text-center py-8 text-slate-500">
                  No catalog tests found.
                </td>
              </tr>
            </ng-template>
          </p-table>
        </div>
      </div>

      <!-- Modal: Add New Lab Test -->
      <p-dialog
        header="Add New Diagnostic Test"
        [(visible)]="displayAddTestDialog"
        [modal]="true"
        [style]="{ width: '500px' }"
        styleClass="p-fluid"
      >
        <form [formGroup]="addTestForm" (ngSubmit)="saveTest()" class="flex flex-col gap-4 mt-2">
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Test Name *</label>
            <input pInputText formControlName="name" placeholder="e.g. Complete Blood Count (CBC)" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Code *</label>
              <input pInputText formControlName="code" placeholder="e.g. CBC-01" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Category *</label>
              <input pInputText formControlName="category" placeholder="e.g. Hematology" />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Reference Range</label>
              <input pInputText formControlName="referenceRange" placeholder="e.g. 4.5-11.0" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Unit</label>
              <input pInputText formControlName="unit" placeholder="e.g. mg/dL" />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Sample Type</label>
              <input pInputText formControlName="sampleType" placeholder="e.g. Whole Blood, Serum" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Standard Price ($) *</label>
              <input pInputText type="number" step="0.01" formControlName="standardPrice" />
            </div>
          </div>

          <div class="flex justify-end gap-2 mt-4">
            <button
              pButton
              type="button"
              label="Cancel"
              class="p-button-text p-button-secondary"
              (click)="displayAddTestDialog = false"
            ></button>
            <button
              pButton
              type="submit"
              label="Save Test"
              class="p-button-primary"
              [disabled]="addTestForm.invalid || submitting()"
            ></button>
          </div>
        </form>
      </p-dialog>

      <!-- Modal: Issue Lab Order -->
      <p-dialog
        header="Issue Patient Lab Order"
        [(visible)]="displayAddOrderDialog"
        [modal]="true"
        [style]="{ width: '500px' }"
        styleClass="p-fluid"
      >
        <form [formGroup]="addOrderForm" (ngSubmit)="saveOrder()" class="flex flex-col gap-4 mt-2">
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Select Test Catalog (Optional)</label>
            <select
              formControlName="labTestId"
              class="w-full p-2 border border-slate-300 rounded-md bg-white text-sm"
              (change)="onCatalogSelect($event)"
            >
              <option value="">-- Custom Diagnostic Test --</option>
              <option *ngFor="let test of catalog()" [value]="test.id">
                {{ test.name }} ({{ test.code }})
              </option>
            </select>
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Diagnostic Test Name *</label>
            <input pInputText formControlName="testName" placeholder="e.g. Lipid Profile Panel" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Ordering Doctor *</label>
              <input pInputText formControlName="orderingDoctorName" placeholder="e.g. Sarah Jenkins" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Priority / Urgency *</label>
              <select formControlName="priority" class="w-full p-2 border border-slate-300 rounded-md bg-white text-sm">
                <option value="Routine">Routine</option>
                <option value="Urgent">Urgent</option>
                <option value="STAT">STAT (Emergency)</option>
              </select>
            </div>
          </div>

          <div class="flex justify-end gap-2 mt-4">
            <button
              pButton
              type="button"
              label="Cancel"
              class="p-button-text p-button-secondary"
              (click)="displayAddOrderDialog = false"
            ></button>
            <button
              pButton
              type="submit"
              label="Issue Order"
              class="p-button-primary"
              [disabled]="addOrderForm.invalid || submitting()"
            ></button>
          </div>
        </form>
      </p-dialog>

      <!-- Modal: Record Lab Results -->
      <p-dialog
        header="Record Diagnostic Result"
        [(visible)]="displayRecordResultDialog"
        [modal]="true"
        [style]="{ width: '450px' }"
        styleClass="p-fluid"
      >
        <div *ngIf="selectedOrderForResult" class="flex flex-col gap-4 mt-2">
          <div class="p-3 bg-slate-50 border border-slate-200 rounded-lg">
            <div class="font-bold text-slate-800">{{ selectedOrderForResult.testName }}</div>
            <div class="text-xs text-slate-600">Patient: <span class="font-bold">{{ selectedOrderForResult.patientName }}</span></div>
            <div class="text-xs text-slate-600">Reference: {{ selectedOrderForResult.referenceRange || 'N/A' }} {{ selectedOrderForResult.unit }}</div>
          </div>

          <form [formGroup]="recordResultForm" (ngSubmit)="saveResult()" class="flex flex-col gap-4">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Observed Result Value *</label>
              <input pInputText formControlName="resultValue" placeholder="e.g. 14.2 or Clear" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Performed By *</label>
              <input pInputText formControlName="performedBy" placeholder="e.g. Lab Technician" />
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-slate-700">Remarks / Lab Notes</label>
              <input pInputText formControlName="remarks" placeholder="Sample hemolyzed slightly" />
            </div>

            <div class="flex items-center gap-2 mt-1">
              <input type="checkbox" id="isAbnormal" formControlName="isAbnormal" class="h-4 w-4 text-rose-600 rounded" />
              <label for="isAbnormal" class="text-xs font-semibold text-rose-700 cursor-pointer">
                Flag Result as Out of Reference Range (Abnormal)
              </label>
            </div>

            <div class="flex justify-end gap-2 mt-4">
              <button
                pButton
                type="button"
                label="Cancel"
                class="p-button-text p-button-secondary"
                (click)="displayRecordResultDialog = false"
              ></button>
              <button
                pButton
                type="submit"
                label="Complete & Save"
                class="p-button-primary"
                [disabled]="recordResultForm.invalid || submitting()"
              ></button>
            </div>
          </form>
        </div>
      </p-dialog>
    </div>
  `
})
export class LaboratoryListComponent implements OnInit {
  private labService = inject(LaboratoryService);
  private fb = inject(FormBuilder);

  // State Signals
  catalog = signal<LabTest[]>([]);
  orders = signal<LabOrder[]>([]);
  loading = signal<boolean>(false);
  submitting = signal<boolean>(false);

  activeTab = signal<'orders' | 'catalog'>('orders');

  catalogSearchTerm = signal<string>('');
  statusFilter = signal<string | null>(null);
  abnormalOnlyFilter = signal<boolean>(false);

  // Dialog visibility flags
  displayAddTestDialog = false;
  displayAddOrderDialog = false;
  displayRecordResultDialog = false;

  selectedOrderForResult: LabOrder | null = null;

  // Forms
  addTestForm = this.fb.group({
    name: ['', Validators.required],
    code: ['', Validators.required],
    category: ['Biochemistry', Validators.required],
    referenceRange: [''],
    unit: [''],
    standardPrice: [50.0, [Validators.required, Validators.min(0)]],
    sampleType: ['Blood']
  });

  addOrderForm = this.fb.group({
    labTestId: [''],
    testName: ['', Validators.required],
    testCode: ['CUSTOM'],
    orderingDoctorName: ['Sarah Jenkins', Validators.required],
    priority: ['Routine', Validators.required]
  });

  recordResultForm = this.fb.group({
    resultValue: ['', Validators.required],
    performedBy: ['Lab Tech', Validators.required],
    remarks: [''],
    isAbnormal: [false]
  });

  // Computed signals
  totalTests = computed(() => this.catalog().length);
  pendingOrdersCount = computed(() => this.orders().filter(o => o.status !== 'Completed').length);
  urgentOrdersCount = computed(() => this.orders().filter(o => o.priority === 'Urgent' || o.priority === 'STAT').length);
  abnormalCount = computed(() => this.orders().filter(o => o.isAbnormal).length);

  filteredCatalog = computed(() => {
    let result = this.catalog();
    const search = this.catalogSearchTerm().trim().toLowerCase();

    if (search) {
      result = result.filter(t =>
        t.name.toLowerCase().includes(search) ||
        t.code.toLowerCase().includes(search) ||
        t.category.toLowerCase().includes(search)
      );
    }

    return result;
  });

  filteredOrders = computed(() => {
    let result = this.orders();
    const status = this.statusFilter();

    if (status) {
      result = result.filter(o => o.status.toLowerCase() === status.toLowerCase());
    }

    if (this.abnormalOnlyFilter()) {
      result = result.filter(o => o.isAbnormal);
    }

    return result;
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.labService.getCatalog().subscribe({
      next: (tests) => {
        this.catalog.set(tests);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });

    this.labService.getOrders().subscribe({
      next: (ordersList) => {
        this.orders.set(ordersList);
      }
    });
  }

  onCatalogSearchInput(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.catalogSearchTerm.set(val);
  }

  setStatusFilter(status: string | null): void {
    this.statusFilter.set(status);
  }

  toggleAbnormalFilter(): void {
    this.abnormalOnlyFilter.set(!this.abnormalOnlyFilter());
  }

  openAddTestDialog(): void {
    this.addTestForm.reset({
      name: '',
      code: '',
      category: 'Biochemistry',
      referenceRange: '',
      unit: '',
      standardPrice: 50.0,
      sampleType: 'Blood'
    });
    this.displayAddTestDialog = true;
  }

  saveTest(): void {
    if (this.addTestForm.invalid) return;

    this.submitting.set(true);
    const val = this.addTestForm.value;
    const req = {
      name: val.name!,
      code: val.code!,
      category: val.category!,
      referenceRange: val.referenceRange || '',
      unit: val.unit || '',
      standardPrice: val.standardPrice!,
      sampleType: val.sampleType || 'Blood'
    };

    this.labService.createLabTest(req).subscribe({
      next: (newTest) => {
        this.catalog.update(list => [newTest, ...list]);
        this.displayAddTestDialog = false;
        this.submitting.set(false);
      },
      error: () => this.submitting.set(false)
    });
  }

  openAddOrderDialog(): void {
    this.addOrderForm.reset({
      labTestId: '',
      testName: '',
      testCode: 'CUSTOM',
      orderingDoctorName: 'Sarah Jenkins',
      priority: 'Routine'
    });
    this.displayAddOrderDialog = true;
  }

  onCatalogSelect(event: Event): void {
    const testId = (event.target as HTMLSelectElement).value;
    if (testId) {
      const selected = this.catalog().find(t => t.id === testId);
      if (selected) {
        this.addOrderForm.patchValue({
          testName: selected.name,
          testCode: selected.code
        });
      }
    }
  }

  saveOrder(): void {
    if (this.addOrderForm.invalid) return;

    this.submitting.set(true);
    const val = this.addOrderForm.value;

    const req = {
      patientId: '00000000-0000-0000-0000-000000000001',
      labTestId: val.labTestId || undefined,
      orderingDoctorName: val.orderingDoctorName!,
      testName: val.testName!,
      testCode: val.testCode || 'CUSTOM',
      priority: val.priority || 'Routine'
    };

    this.labService.createOrder(req).subscribe({
      next: (newOrder) => {
        this.orders.update(list => [newOrder, ...list]);
        this.displayAddOrderDialog = false;
        this.submitting.set(false);
      },
      error: () => this.submitting.set(false)
    });
  }

  openRecordResultDialog(order: LabOrder): void {
    this.selectedOrderForResult = order;
    this.recordResultForm.reset({
      resultValue: '',
      performedBy: 'Lab Tech',
      remarks: '',
      isAbnormal: false
    });
    this.displayRecordResultDialog = true;
  }

  saveResult(): void {
    if (!this.selectedOrderForResult || this.recordResultForm.invalid) return;

    this.submitting.set(true);
    const val = this.recordResultForm.value;
    const req = {
      resultValue: val.resultValue!,
      performedBy: val.performedBy!,
      remarks: val.remarks || '',
      isAbnormal: !!val.isAbnormal
    };

    this.labService.recordResult(this.selectedOrderForResult.id, req).subscribe({
      next: (updatedOrder) => {
        this.orders.update(list => list.map(o => o.id === updatedOrder.id ? updatedOrder : o));
        this.displayRecordResultDialog = false;
        this.submitting.set(false);
      },
      error: () => this.submitting.set(false)
    });
  }

  getPrioritySeverity(priority: string): 'danger' | 'warn' | 'info' {
    switch (priority.toUpperCase()) {
      case 'STAT': return 'danger';
      case 'URGENT': return 'warn';
      default: return 'info';
    }
  }

  getStatusSeverity(status: string): 'success' | 'warn' | 'danger' | 'info' {
    switch (status.toLowerCase()) {
      case 'completed': return 'success';
      case 'inprogress': return 'info';
      case 'cancelled': return 'danger';
      default: return 'warn';
    }
  }
}
