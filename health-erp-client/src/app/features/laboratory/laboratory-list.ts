import { Component, inject, OnInit, signal, computed, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { LaboratoryService } from './data-access/laboratory.service';
import { LabTest, LabOrder } from './data-access/laboratory.model';
import { PatientService } from '../patients/data-access/patient.service';
import { Patient } from '../patients/data-access/patient.model';
import { StaffService } from '../staff/data-access/staff.service';
import { Staff } from '../staff/data-access/staff.model';
import { AuthService } from '../../core/services/auth.service';
import { Roles } from '../../core/constants/roles';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-laboratory-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './laboratory-list.html',
})
export class LaboratoryListComponent implements OnInit {
  private labService = inject(LaboratoryService);
  private patientService = inject(PatientService);
  private staffService = inject(StaffService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);

  get canOrderLabTest(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.Doctor]);
  }

  get canEnterLabResult(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.LabTechnician]);
  }

  get canManageCatalog(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.LabTechnician]);
  }

  // Global Math for template expressions
  Math = Math;

  // State Signals
  catalog = signal<LabTest[]>([]);
  orders = signal<LabOrder[]>([]);
  patientList = signal<Patient[]>([]);
  staffList = signal<Staff[]>([]);
  loading = signal<boolean>(false);
  submitting = signal<boolean>(false);

  // Navigation
  activeTab = signal<'orders' | 'catalog'>('orders');

  // Filters
  orderSearchQuery = signal<string>('');
  catalogSearchTerm = signal<string>('');
  statusFilter = signal<string | null>(null);
  priorityOnlyFilter = signal<boolean>(false);
  abnormalOnlyFilter = signal<boolean>(false);

  // Pagination
  orderCurrentPage = signal<number>(1);
  orderPageSize = signal<number>(10);
  catalogCurrentPage = signal<number>(1);
  catalogPageSize = signal<number>(10);

  // Dialog visibility flags
  displayAddTestDialog = false;
  displayAddOrderDialog = false;
  displayRecordResultDialog = false;

  selectedOrderForResult: LabOrder | null = null;

  // Form submission feedback flags
  addTestSubmitted = signal<boolean>(false);
  addTestError = signal<string | null>(null);

  addOrderSubmitted = signal<boolean>(false);
  addOrderError = signal<string | null>(null);

  // Forms
  addTestForm = this.fb.group({
    name: ['', Validators.required],
    code: ['', Validators.required],
    category: ['Biochemistry', Validators.required],
    referenceRange: [''],
    unit: [''],
    standardPrice: [50.0, [Validators.required, Validators.min(0)]],
    sampleType: ['Whole Blood'],
  });

  addOrderForm = this.fb.group({
    patientId: ['', Validators.required],
    labTestId: [''],
    testName: ['', Validators.required],
    testCode: ['CUSTOM'],
    orderingDoctorName: ['Sarah Jenkins', Validators.required],
    priority: ['Routine', Validators.required],
  });

  recordResultForm = this.fb.group({
    resultValue: ['', Validators.required],
    performedBy: ['Lab Tech', Validators.required],
    remarks: [''],
    isAbnormal: [false],
  });

  // Computed KPI Signals
  totalTests = computed(() => this.catalog().length);
  pendingOrdersCount = computed(() => this.orders().filter((o) => o.status !== 'Completed').length);
  urgentOrdersCount = computed(() => this.orders().filter((o) => o.priority === 'Urgent' || o.priority === 'STAT').length);
  abnormalCount = computed(() => this.orders().filter((o) => o.isAbnormal).length);

  // Patient Map for fast MRN Lookup
  private patientMap = computed(() => {
    const map = new Map<string, Patient>();
    for (const p of this.patientList()) {
      map.set(p.id, p);
    }
    return map;
  });

  // Filtered Orders List
  filteredOrders = computed(() => {
    let result = this.orders();
    const query = this.orderSearchQuery().trim().toLowerCase();

    if (query) {
      result = result.filter(
        (o) =>
          o.patientName.toLowerCase().includes(query) ||
          o.testName.toLowerCase().includes(query) ||
          o.testCode.toLowerCase().includes(query) ||
          o.orderingDoctorName.toLowerCase().includes(query)
      );
    }

    const status = this.statusFilter();
    if (status) {
      result = result.filter((o) => o.status.toLowerCase() === status.toLowerCase());
    }

    if (this.priorityOnlyFilter()) {
      result = result.filter((o) => o.priority === 'STAT' || o.priority === 'Urgent');
    }

    if (this.abnormalOnlyFilter()) {
      result = result.filter((o) => o.isAbnormal);
    }

    return result;
  });

  // Paginated Orders List
  paginatedOrders = computed(() => {
    const list = this.filteredOrders();
    const page = this.orderCurrentPage();
    const size = this.orderPageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  orderTotalPages = computed(() => {
    const count = this.filteredOrders().length;
    return Math.ceil(count / this.orderPageSize()) || 1;
  });

  // Filtered Catalog List
  filteredCatalog = computed(() => {
    let result = this.catalog();
    const search = this.catalogSearchTerm().trim().toLowerCase();

    if (search) {
      result = result.filter(
        (t) =>
          t.name.toLowerCase().includes(search) ||
          t.code.toLowerCase().includes(search) ||
          t.category.toLowerCase().includes(search)
      );
    }

    return result;
  });

  // Paginated Catalog List
  paginatedCatalog = computed(() => {
    const list = this.filteredCatalog();
    const page = this.catalogCurrentPage();
    const size = this.catalogPageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  catalogTotalPages = computed(() => {
    const count = this.filteredCatalog().length;
    return Math.ceil(count / this.catalogPageSize()) || 1;
  });

  ngOnInit(): void {
    this.loadData();
  }

  @HostListener('window:keydown.escape')
  onEscapeKey(): void {
    if (this.displayAddTestDialog) this.displayAddTestDialog = false;
    if (this.displayAddOrderDialog) this.displayAddOrderDialog = false;
    if (this.displayRecordResultDialog) this.displayRecordResultDialog = false;
    document.body.style.overflow = '';
  }

  loadData(): void {
    this.loading.set(true);
    this.labService.getCatalog().subscribe({
      next: (tests) => {
        this.catalog.set(tests || []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.labService.getOrders().subscribe({
      next: (ordersList) => {
        this.orders.set(ordersList || []);
      },
    });

    this.patientService.getPatients().subscribe({
      next: (patientsList) => {
        this.patientList.set(patientsList || []);
      },
    });

    this.staffService.getStaff().subscribe({
      next: (staff) => {
        this.staffList.set(staff || []);
      },
    });
  }

  // Workspace Navigation & KPI Click Actions
  setActiveTab(tab: 'orders' | 'catalog'): void {
    this.activeTab.set(tab);
  }

  onKpiClick(type: 'catalog' | 'pending' | 'urgent' | 'abnormal'): void {
    if (type === 'catalog') {
      this.activeTab.set('catalog');
    } else if (type === 'pending') {
      this.activeTab.set('orders');
      this.statusFilter.set('Ordered');
      this.priorityOnlyFilter.set(false);
      this.abnormalOnlyFilter.set(false);
      this.orderCurrentPage.set(1);
    } else if (type === 'urgent') {
      this.activeTab.set('orders');
      this.priorityOnlyFilter.set(true);
      this.statusFilter.set(null);
      this.abnormalOnlyFilter.set(false);
      this.orderCurrentPage.set(1);
    } else if (type === 'abnormal') {
      this.activeTab.set('orders');
      this.abnormalOnlyFilter.set(true);
      this.statusFilter.set(null);
      this.priorityOnlyFilter.set(false);
      this.orderCurrentPage.set(1);
    }
  }

  setStatusFilter(status: string | null): void {
    this.statusFilter.set(status);
    this.priorityOnlyFilter.set(false);
    this.orderCurrentPage.set(1);
  }

  toggleAbnormalFilter(): void {
    this.abnormalOnlyFilter.set(!this.abnormalOnlyFilter());
    this.orderCurrentPage.set(1);
  }

  clearOrderFilters(): void {
    this.orderSearchQuery.set('');
    this.statusFilter.set(null);
    this.priorityOnlyFilter.set(false);
    this.abnormalOnlyFilter.set(false);
    this.orderCurrentPage.set(1);
  }

  goToOrderPage(page: number): void {
    if (page >= 1 && page <= this.orderTotalPages()) {
      this.orderCurrentPage.set(page);
    }
  }

  goToCatalogPage(page: number): void {
    if (page >= 1 && page <= this.catalogTotalPages()) {
      this.catalogCurrentPage.set(page);
    }
  }

  onBackdropClick(event: MouseEvent, dialogType: 'addTest' | 'addOrder' | 'recordResult'): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      if (dialogType === 'addTest') this.displayAddTestDialog = false;
      if (dialogType === 'addOrder') this.displayAddOrderDialog = false;
      if (dialogType === 'recordResult') this.displayRecordResultDialog = false;
      document.body.style.overflow = '';
    }
  }

  // Add Catalog Test Dialog Logic
  openAddTestDialog(): void {
    this.addTestSubmitted.set(false);
    this.addTestError.set(null);
    this.addTestForm.reset({
      name: '',
      code: '',
      category: 'Biochemistry',
      referenceRange: '',
      unit: '',
      standardPrice: 50.0,
      sampleType: 'Whole Blood',
    });
    this.displayAddTestDialog = true;
    document.body.style.overflow = 'hidden';
  }

  saveTest(): void {
    this.addTestSubmitted.set(true);

    if (this.addTestForm.invalid) {
      return;
    }

    this.submitting.set(true);
    this.addTestError.set(null);
    const val = this.addTestForm.value;

    const req = {
      name: val.name!.trim(),
      code: val.code!.trim(),
      category: val.category!.trim(),
      referenceRange: val.referenceRange?.trim() || '',
      unit: val.unit?.trim() || '',
      standardPrice: Number(val.standardPrice),
      sampleType: val.sampleType || 'Whole Blood',
    };

    this.labService.createLabTest(req).subscribe({
      next: (newTest) => {
        this.catalog.update((list) => [newTest, ...list]);
        this.displayAddTestDialog = false;
        this.submitting.set(false);
        document.body.style.overflow = '';

        Swal.fire({
          title: 'Test Cataloged',
          text: `Diagnostic test ${newTest.name} has been added to the catalog.`,
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2200,
          timerProgressBar: true,
        });
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err.error?.detail || err.error?.message || 'Failed to add catalog test.';
        this.addTestError.set(msg);
      },
    });
  }

  // Issue Order Dialog Logic
  openAddOrderDialog(): void {
    this.addOrderSubmitted.set(false);
    this.addOrderError.set(null);

    const defaultPatient = this.patientList().length > 0 ? this.patientList()[0].id : '';
    const defaultDoctor = this.staffList().length > 0 ? `Dr. ${this.staffList()[0].firstName} ${this.staffList()[0].lastName}` : 'Dr. Sarah Jenkins';

    this.addOrderForm.reset({
      patientId: defaultPatient,
      labTestId: '',
      testName: '',
      testCode: 'CUSTOM',
      orderingDoctorName: defaultDoctor,
      priority: 'Routine',
    });
    this.displayAddOrderDialog = true;
    document.body.style.overflow = 'hidden';
  }

  onCatalogSelect(event: Event): void {
    const testId = (event.target as HTMLSelectElement).value;
    if (testId) {
      const selected = this.catalog().find((t) => t.id === testId);
      if (selected) {
        this.addOrderForm.patchValue({
          testName: selected.name,
          testCode: selected.code,
        });
      }
    }
  }

  saveOrder(): void {
    this.addOrderSubmitted.set(true);

    if (this.addOrderForm.invalid) {
      return;
    }

    this.submitting.set(true);
    this.addOrderError.set(null);
    const val = this.addOrderForm.value;

    const req = {
      patientId: val.patientId!,
      labTestId: val.labTestId || undefined,
      orderingDoctorName: val.orderingDoctorName!.replace(/^Dr\.\s*/i, ''),
      testName: val.testName!.trim(),
      testCode: val.testCode?.trim() || 'CUSTOM',
      priority: val.priority || 'Routine',
    };

    this.labService.createOrder(req).subscribe({
      next: (newOrder) => {
        this.orders.update((list) => [newOrder, ...list]);
        this.displayAddOrderDialog = false;
        this.submitting.set(false);
        document.body.style.overflow = '';

        Swal.fire({
          title: 'Order Issued',
          text: `Diagnostic order for ${newOrder.testName} has been created successfully.`,
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2200,
          timerProgressBar: true,
        });
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err.error?.detail || err.error?.message || 'Failed to issue diagnostic order.';
        this.addOrderError.set(msg);
      },
    });
  }

  // Record Result Dialog Logic
  openRecordResultDialog(order: LabOrder): void {
    this.selectedOrderForResult = order;
    this.recordResultForm.reset({
      resultValue: '',
      performedBy: 'Lab Tech',
      remarks: '',
      isAbnormal: false,
    });
    this.displayRecordResultDialog = true;
    document.body.style.overflow = 'hidden';
  }

  saveResult(): void {
    if (!this.selectedOrderForResult || this.recordResultForm.invalid) return;

    this.submitting.set(true);
    const val = this.recordResultForm.value;
    const req = {
      resultValue: val.resultValue!.trim(),
      performedBy: val.performedBy!.trim(),
      remarks: val.remarks?.trim() || '',
      isAbnormal: !!val.isAbnormal,
    };

    this.labService.recordResult(this.selectedOrderForResult.id, req).subscribe({
      next: (updatedOrder) => {
        this.orders.update((list) => list.map((o) => (o.id === updatedOrder.id ? updatedOrder : o)));
        this.displayRecordResultDialog = false;
        this.submitting.set(false);
        document.body.style.overflow = '';

        Swal.fire({
          title: 'Result Recorded',
          text: `Result for ${updatedOrder.testName} has been saved successfully.`,
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2200,
          timerProgressBar: true,
        });
      },
      error: () => this.submitting.set(false),
    });
  }

  // Formatting & Display Helpers
  getPatientMRN(order: LabOrder): string | null {
    if (!order.patientId) return null;
    const p = this.patientMap().get(order.patientId);
    return p?.medicalRecordNumber || null;
  }

  /**
   * Reference Range Unit Duplication Fix:
   * Prevents displaying duplicated unit strings like "70–99 mg/dL mg/dL" or "< 5.7 % %"
   */
  formatReferenceRange(referenceRange?: string, unit?: string): string {
    if (!referenceRange || referenceRange.trim() === '') {
      return unit ? unit.trim() : 'N/A';
    }

    const refTrimmed = referenceRange.trim();
    if (!unit || unit.trim() === '') {
      return refTrimmed;
    }

    const unitTrimmed = unit.trim();

    // If referenceRange already contains or ends with unit string (case insensitive)
    if (
      refTrimmed.toLowerCase().endsWith(unitTrimmed.toLowerCase()) ||
      refTrimmed.toLowerCase().includes(unitTrimmed.toLowerCase())
    ) {
      return refTrimmed;
    }

    return `${refTrimmed} ${unitTrimmed}`;
  }

  getPriorityBadgeClass(priority: string): string {
    switch (priority?.toUpperCase()) {
      case 'STAT':
        return 'bg-rose-100 text-rose-800 border border-rose-300 font-extrabold animate-pulse';
      case 'URGENT':
        return 'bg-amber-50 text-amber-800 border border-amber-200 font-bold';
      case 'ROUTINE':
      default:
        return 'bg-blue-50 text-blue-700 border border-blue-200 font-semibold';
    }
  }

  getOrderStatusBadgeClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'completed':
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      case 'inprogress':
      case 'in progress':
        return 'bg-blue-50 text-blue-700 border border-blue-200';
      case 'cancelled':
        return 'bg-rose-50 text-rose-700 border border-rose-200';
      case 'ordered':
      case 'pending':
      default:
        return 'bg-amber-50 text-amber-700 border border-amber-200';
    }
  }

  getDisplayStatus(status: string): string {
    switch (status?.toLowerCase()) {
      case 'ordered':
        return 'Ordered / Pending';
      case 'inprogress':
        return 'In Progress';
      default:
        return status;
    }
  }
}
