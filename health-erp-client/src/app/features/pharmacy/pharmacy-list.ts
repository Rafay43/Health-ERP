import { Component, inject, OnInit, signal, computed, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { PharmacyService } from './data-access/pharmacy.service';
import { Drug, Prescription } from './data-access/pharmacy.model';
import { PatientService } from '../patients/data-access/patient.service';
import { Patient } from '../patients/data-access/patient.model';
import { StaffService } from '../staff/data-access/staff.service';
import { Staff } from '../staff/data-access/staff.model';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-pharmacy-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './pharmacy-list.html',
})
export class PharmacyListComponent implements OnInit {
  private pharmacyService = inject(PharmacyService);
  private patientService = inject(PatientService);
  private staffService = inject(StaffService);
  private fb = inject(FormBuilder);

  // Global Math for template expressions
  Math = Math;

  // State Signals
  drugs = signal<Drug[]>([]);
  prescriptions = signal<Prescription[]>([]);
  patientList = signal<Patient[]>([]);
  staffList = signal<Staff[]>([]);
  loading = signal<boolean>(false);
  submitting = signal<boolean>(false);

  // Workspace Navigation
  activeTab = signal<'inventory' | 'prescriptions'>('inventory');

  // Filters
  searchTerm = signal<string>('');
  lowStockFilter = signal<boolean>(false);
  expiringFilter = signal<boolean>(false);
  prescriptionStatusFilter = signal<string | null>(null);

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);
  rxCurrentPage = signal<number>(1);
  rxPageSize = signal<number>(10);

  // Dialog Visibility Flags
  displayAddDrugDialog = false;
  displayAdjustStockDialog = false;
  displayAddPrescriptionDialog = false;

  // Stock Adjustment State
  selectedDrugForStock: Drug | null = null;
  newStockValue = 0;

  // Form Submission Feedback Flags
  addDrugSubmitted = signal<boolean>(false);
  addDrugError = signal<string | null>(null);

  addRxSubmitted = signal<boolean>(false);
  addRxError = signal<string | null>(null);

  // Forms
  addDrugForm = this.fb.group({
    name: ['', Validators.required],
    genericName: [''],
    category: ['General', Validators.required],
    stockQuantity: [100, [Validators.required, Validators.min(0)]],
    reorderLevel: [20, [Validators.required, Validators.min(0)]],
    expiryDate: [
      new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString().substring(0, 10),
      Validators.required,
    ],
    unitPrice: [10.0, [Validators.required, Validators.min(0)]],
  });

  addPrescriptionForm = this.fb.group({
    patientId: ['', Validators.required],
    drugId: [''],
    doctorName: ['Sarah Jenkins', Validators.required],
    medicationName: ['', Validators.required],
    dosage: ['500mg', Validators.required],
    frequency: ['Twice daily', Validators.required],
    durationDays: [7, [Validators.required, Validators.min(1)]],
    refills: [0, [Validators.required, Validators.min(0)]],
    instructions: ['Take after meals with plenty of water'],
  });

  // Computed KPI Signals
  totalSkus = computed(() => this.drugs().length);
  lowStockCount = computed(() => this.drugs().filter((d) => d.isLowStock).length);
  expiringCount = computed(() => this.drugs().filter((d) => d.isExpiringSoon).length);
  pendingPrescriptionsCount = computed(() => this.prescriptions().filter((p) => p.status === 'Pending').length);

  // Patient Map for fast MRN Lookup
  private patientMap = computed(() => {
    const map = new Map<string, Patient>();
    for (const p of this.patientList()) {
      map.set(p.id, p);
    }
    return map;
  });

  // Filtered Inventory List
  filteredDrugs = computed(() => {
    let result = this.drugs();
    const search = this.searchTerm().trim().toLowerCase();

    if (search) {
      result = result.filter(
        (d) =>
          d.name.toLowerCase().includes(search) ||
          d.genericName.toLowerCase().includes(search) ||
          d.category.toLowerCase().includes(search)
      );
    }

    if (this.lowStockFilter()) {
      result = result.filter((d) => d.isLowStock);
    }

    if (this.expiringFilter()) {
      result = result.filter((d) => d.isExpiringSoon);
    }

    return result;
  });

  // Paginated Inventory List
  paginatedDrugs = computed(() => {
    const list = this.filteredDrugs();
    const page = this.currentPage();
    const size = this.pageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  totalPages = computed(() => {
    const count = this.filteredDrugs().length;
    return Math.ceil(count / this.pageSize()) || 1;
  });

  // Filtered Prescriptions List
  filteredPrescriptions = computed(() => {
    let result = this.prescriptions();
    const status = this.prescriptionStatusFilter();

    if (status) {
      result = result.filter((p) => p.status.toLowerCase() === status.toLowerCase());
    }

    return result;
  });

  // Paginated Prescriptions List
  paginatedPrescriptions = computed(() => {
    const list = this.filteredPrescriptions();
    const page = this.rxCurrentPage();
    const size = this.rxPageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  rxTotalPages = computed(() => {
    const count = this.filteredPrescriptions().length;
    return Math.ceil(count / this.rxPageSize()) || 1;
  });

  ngOnInit(): void {
    this.loadData();
  }

  @HostListener('window:keydown.escape')
  onEscapeKey(): void {
    if (this.displayAddDrugDialog) this.displayAddDrugDialog = false;
    if (this.displayAdjustStockDialog) this.displayAdjustStockDialog = false;
    if (this.displayAddPrescriptionDialog) this.displayAddPrescriptionDialog = false;
    document.body.style.overflow = '';
  }

  loadData(): void {
    this.loading.set(true);
    this.pharmacyService.getInventory().subscribe({
      next: (inventory) => {
        this.drugs.set(inventory || []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.pharmacyService.getPrescriptions().subscribe({
      next: (rxs) => {
        this.prescriptions.set(rxs || []);
      },
    });

    this.patientService.getPatients().subscribe({
      next: (pts) => {
        this.patientList.set(pts || []);
      },
    });

    this.staffService.getStaff().subscribe({
      next: (staff) => {
        this.staffList.set(staff || []);
      },
    });
  }

  // Navigation & Interactive KPI Actions
  setActiveTab(tab: 'inventory' | 'prescriptions'): void {
    this.activeTab.set(tab);
  }

  onKpiClick(type: 'lowStock' | 'expiring' | 'pending'): void {
    if (type === 'lowStock') {
      this.activeTab.set('inventory');
      this.lowStockFilter.set(true);
      this.expiringFilter.set(false);
      this.currentPage.set(1);
    } else if (type === 'expiring') {
      this.activeTab.set('inventory');
      this.expiringFilter.set(true);
      this.lowStockFilter.set(false);
      this.currentPage.set(1);
    } else if (type === 'pending') {
      this.activeTab.set('prescriptions');
      this.prescriptionStatusFilter.set('Pending');
      this.rxCurrentPage.set(1);
    }
  }

  toggleLowStockFilter(): void {
    this.lowStockFilter.set(!this.lowStockFilter());
    this.currentPage.set(1);
  }

  toggleExpiringFilter(): void {
    this.expiringFilter.set(!this.expiringFilter());
    this.currentPage.set(1);
  }

  clearInventoryFilters(): void {
    this.searchTerm.set('');
    this.lowStockFilter.set(false);
    this.expiringFilter.set(false);
    this.currentPage.set(1);
  }

  setPrescriptionStatusFilter(status: string | null): void {
    this.prescriptionStatusFilter.set(status);
    this.rxCurrentPage.set(1);
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  goToRxPage(page: number): void {
    if (page >= 1 && page <= this.rxTotalPages()) {
      this.rxCurrentPage.set(page);
    }
  }

  onBackdropClick(event: MouseEvent, dialogType: 'addDrug' | 'adjustStock' | 'addPrescription'): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      if (dialogType === 'addDrug') this.displayAddDrugDialog = false;
      if (dialogType === 'adjustStock') this.displayAdjustStockDialog = false;
      if (dialogType === 'addPrescription') this.displayAddPrescriptionDialog = false;
      document.body.style.overflow = '';
    }
  }

  // Add Medication Dialog Logic
  openAddDrugDialog(): void {
    this.addDrugSubmitted.set(false);
    this.addDrugError.set(null);
    this.addDrugForm.reset({
      name: '',
      genericName: '',
      category: 'General',
      stockQuantity: 100,
      reorderLevel: 20,
      expiryDate: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString().substring(0, 10),
      unitPrice: 10.0,
    });
    this.displayAddDrugDialog = true;
    document.body.style.overflow = 'hidden';
  }

  saveDrug(): void {
    this.addDrugSubmitted.set(true);

    if (this.addDrugForm.invalid) {
      return;
    }

    this.submitting.set(true);
    this.addDrugError.set(null);
    const val = this.addDrugForm.value;

    const req = {
      name: val.name!.trim(),
      genericName: val.genericName?.trim() || '',
      category: val.category!.trim(),
      stockQuantity: Number(val.stockQuantity),
      reorderLevel: Number(val.reorderLevel),
      expiryDateUtc: new Date(val.expiryDate!).toISOString(),
      unitPrice: Number(val.unitPrice),
    };

    this.pharmacyService.createDrug(req).subscribe({
      next: (newDrug) => {
        this.drugs.update((list) => [newDrug, ...list]);
        this.displayAddDrugDialog = false;
        this.submitting.set(false);
        document.body.style.overflow = '';

        Swal.fire({
          title: 'Medication Added',
          text: `Medication ${newDrug.name} has been added to pharmacy inventory.`,
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2200,
          timerProgressBar: true,
        });
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err.error?.detail || err.error?.message || 'Failed to add medication. Please check field values.';
        this.addDrugError.set(msg);
      },
    });
  }

  // Adjust Stock Dialog Logic
  openAdjustStockDialog(drug: Drug): void {
    this.selectedDrugForStock = drug;
    this.newStockValue = drug.stockQuantity;
    this.displayAdjustStockDialog = true;
    document.body.style.overflow = 'hidden';
  }

  saveAdjustedStock(): void {
    if (!this.selectedDrugForStock || this.newStockValue < 0) return;

    this.submitting.set(true);
    const targetId = this.selectedDrugForStock.id;

    this.pharmacyService.updateStock(targetId, Number(this.newStockValue)).subscribe({
      next: (updated) => {
        this.drugs.update((list) => list.map((d) => (d.id === updated.id ? updated : d)));
        this.displayAdjustStockDialog = false;
        this.submitting.set(false);
        document.body.style.overflow = '';

        Swal.fire({
          title: 'Stock Updated',
          text: `Stock quantity for ${updated.name} updated to ${updated.stockQuantity} units.`,
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2200,
          timerProgressBar: true,
        });
      },
      error: () => {
        this.submitting.set(false);
      },
    });
  }

  // Create Prescription Dialog Logic
  openAddPrescriptionDialog(): void {
    this.addRxSubmitted.set(false);
    this.addRxError.set(null);

    const firstPatientId = this.patientList().length > 0 ? this.patientList()[0].id : '';
    const defaultDoctor = this.staffList().length > 0 ? `Dr. ${this.staffList()[0].firstName} ${this.staffList()[0].lastName}` : 'Dr. Sarah Jenkins';

    this.addPrescriptionForm.reset({
      patientId: firstPatientId,
      drugId: '',
      doctorName: defaultDoctor,
      medicationName: '',
      dosage: '500mg',
      frequency: 'Twice daily',
      durationDays: 7,
      refills: 0,
      instructions: 'Take after meals with plenty of water',
    });
    this.displayAddPrescriptionDialog = true;
    document.body.style.overflow = 'hidden';
  }

  onDrugSelect(event: Event): void {
    const drugId = (event.target as HTMLSelectElement).value;
    if (drugId) {
      const selected = this.drugs().find((d) => d.id === drugId);
      if (selected) {
        this.addPrescriptionForm.patchValue({
          medicationName: selected.name,
        });
      }
    }
  }

  savePrescription(): void {
    this.addRxSubmitted.set(true);

    if (this.addPrescriptionForm.invalid) {
      return;
    }

    this.submitting.set(true);
    this.addRxError.set(null);
    const val = this.addPrescriptionForm.value;

    const req = {
      patientId: val.patientId!,
      drugId: val.drugId || undefined,
      doctorName: val.doctorName!.replace(/^Dr\.\s*/i, ''),
      medicationName: val.medicationName!.trim(),
      dosage: val.dosage!.trim(),
      frequency: val.frequency!.trim(),
      durationDays: Number(val.durationDays) || 7,
      refills: Number(val.refills) || 0,
      instructions: val.instructions?.trim() || '',
    };

    this.pharmacyService.createPrescription(req).subscribe({
      next: (newRx) => {
        this.prescriptions.update((list) => [newRx, ...list]);
        this.displayAddPrescriptionDialog = false;
        this.submitting.set(false);
        document.body.style.overflow = '';

        Swal.fire({
          title: 'Prescription Issued',
          text: `Prescription order for ${newRx.medicationName} has been issued successfully.`,
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2200,
          timerProgressBar: true,
        });
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err.error?.detail || err.error?.message || 'Failed to issue prescription order.';
        this.addRxError.set(msg);
      },
    });
  }

  // Dispense Action
  dispensePrescription(rx: Prescription): void {
    this.submitting.set(true);
    this.pharmacyService.dispensePrescription(rx.id).subscribe({
      next: (dispensedRx) => {
        this.prescriptions.update((list) => list.map((p) => (p.id === dispensedRx.id ? dispensedRx : p)));
        this.submitting.set(false);

        // Refresh inventory data to reflect deducted stock (backend logic)
        this.loadData();

        Swal.fire({
          title: 'Prescription Dispensed',
          text: `Prescription for ${dispensedRx.medicationName} dispensed successfully.`,
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
  getPatientMRN(rx: Prescription): string | null {
    if (!rx.patientId) return null;
    const p = this.patientMap().get(rx.patientId);
    return p?.medicalRecordNumber || null;
  }

  formatDate(isoString: string): string {
    if (!isoString) return '';
    const d = new Date(isoString);
    if (isNaN(d.getTime())) return isoString;
    return d.toLocaleDateString('en-US', {
      month: 'short',
      day: '2-digit',
      year: 'numeric',
    });
  }

  getRxStatusClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'dispensed':
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      case 'cancelled':
        return 'bg-rose-50 text-rose-700 border border-rose-200';
      case 'pending':
      default:
        return 'bg-amber-50 text-amber-700 border border-amber-200';
    }
  }
}
