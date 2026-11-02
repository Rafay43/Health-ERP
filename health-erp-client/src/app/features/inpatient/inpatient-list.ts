import { Component, inject, OnInit, signal, computed, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { InpatientService } from './data-access/inpatient.service';
import { Bed, Admission } from './data-access/inpatient.model';
import { PatientService } from '../patients/data-access/patient.service';
import { Patient } from '../patients/data-access/patient.model';
import { StaffService } from '../staff/data-access/staff.service';
import { Staff } from '../staff/data-access/staff.model';
import { AuthService } from '../../core/services/auth.service';
import { Roles } from '../../core/constants/roles';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-inpatient-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './inpatient-list.html',
})
export class InpatientListComponent implements OnInit {
  private inpatientService = inject(InpatientService);
  private patientService = inject(PatientService);
  private staffService = inject(StaffService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);

  get canAddBed(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.Nurse]);
  }

  get canAdmit(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.Doctor, Roles.Nurse]);
  }

  get canTransfer(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.Doctor, Roles.Nurse]);
  }

  get canDischarge(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.Doctor]);
  }

  // Global Math for template expressions
  Math = Math;

  // State Signals
  beds = signal<Bed[]>([]);
  admissions = signal<Admission[]>([]);
  availablePatients = signal<Patient[]>([]);
  staffList = signal<Staff[]>([]);
  loading = signal<boolean>(false);
  submitting = signal<boolean>(false);

  // Navigation & Filters
  activeTab = signal<'grid' | 'admissions'>('grid');
  searchQuery = signal<string>('');
  selectedWard = signal<string>('');
  selectedStatus = signal<string>('');
  activeOnlyFilter = signal<boolean>(false);

  // Pagination for Admissions
  admCurrentPage = signal<number>(1);
  admPageSize = signal<number>(10);

  // Dialog Visibility Flags
  showAddBedDialog = signal<boolean>(false);
  showAdmitDialog = signal<boolean>(false);
  showDischargeDialog = signal<boolean>(false);
  showTransferDialog = signal<boolean>(false);

  selectedAdmissionForDischarge = signal<Admission | null>(null);
  selectedAdmissionForTransfer = signal<Admission | null>(null);

  // Form submission error feedback flags
  addBedError = signal<string | null>(null);
  admitError = signal<string | null>(null);

  // Reactive Forms
  bedForm = this.fb.group({
    wardName: ['', Validators.required],
    roomNumber: ['', Validators.required],
    bedNumber: ['', Validators.required],
    bedType: ['Standard Hospital Bed', Validators.required],
    dailyRate: [150, [Validators.required, Validators.min(0)]],
  });

  admitForm = this.fb.group({
    patientId: ['', Validators.required],
    bedId: ['', Validators.required],
    admittingDoctorName: ['Demo Clinician', Validators.required],
    chiefComplaint: ['', Validators.required],
  });

  dischargeForm = this.fb.group({
    dischargeSummary: ['Patient stabilized and discharged home with follow-up instructions.', Validators.required],
  });

  transferForm = this.fb.group({
    newBedId: ['', Validators.required],
    transferReason: ['Transferred for specialized care monitoring.', Validators.required],
  });

  // Computed KPI Signals
  totalBedsCount = computed(() => this.beds().length);
  occupiedBedsCount = computed(() => this.beds().filter((b) => b.status === 'Occupied').length);
  availableBedsCount = computed(() => this.beds().filter((b) => b.status === 'Available').length);

  occupancyRate = computed(() => {
    const total = this.totalBedsCount();
    if (total === 0) return 0;
    return Math.round((this.occupiedBedsCount() / total) * 100);
  });

  availableRate = computed(() => {
    const total = this.totalBedsCount();
    if (total === 0) return 0;
    return Math.round((this.availableBedsCount() / total) * 100);
  });

  activeAdmissionsCount = computed(() => this.admissions().filter((a) => !a.isDischarged).length);

  wardList = computed(() => {
    const wards = new Set(this.beds().map((b) => b.wardName));
    return Array.from(wards).sort();
  });

  getWardBedCount(wardName: string): number {
    return this.beds().filter((b) => b.wardName === wardName).length;
  }

  // Filtered Bed Grid List
  filteredBeds = computed(() => {
    let list = this.beds();

    const query = this.searchQuery().trim().toLowerCase();
    if (query) {
      list = list.filter(
        (b) =>
          b.roomNumber?.toLowerCase().includes(query) ||
          b.bedNumber?.toLowerCase().includes(query) ||
          b.wardName?.toLowerCase().includes(query) ||
          b.occupiedByPatientName?.toLowerCase().includes(query) ||
          b.occupiedByPatientMrn?.toLowerCase().includes(query)
      );
    }

    const ward = this.selectedWard();
    if (ward) {
      list = list.filter((b) => b.wardName.toLowerCase() === ward.toLowerCase());
    }

    const status = this.selectedStatus();
    if (status) {
      list = list.filter((b) => b.status.toLowerCase() === status.toLowerCase());
    }

    return list;
  });

  availableBedsList = computed(() => this.beds().filter((b) => b.status === 'Available'));

  // Paginated Admissions List
  paginatedAdmissions = computed(() => {
    const list = this.admissions();
    const page = this.admCurrentPage();
    const size = this.admPageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  admTotalPages = computed(() => {
    const count = this.admissions().length;
    return Math.ceil(count / this.admPageSize()) || 1;
  });

  ngOnInit(): void {
    this.loadBeds();
    this.loadAdmissions();
    this.loadPatients();
    this.loadStaff();
  }

  @HostListener('window:keydown.escape')
  onEscapeKey(): void {
    if (this.showAddBedDialog()) this.showAddBedDialog.set(false);
    if (this.showAdmitDialog()) this.showAdmitDialog.set(false);
    if (this.showDischargeDialog()) this.showDischargeDialog.set(false);
    if (this.showTransferDialog()) this.showTransferDialog.set(false);
    document.body.style.overflow = '';
  }

  loadBeds(): void {
    this.loading.set(true);
    this.inpatientService.getBedGrid().subscribe({
      next: (data) => {
        this.beds.set(data || []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  loadAdmissions(): void {
    this.inpatientService.getAdmissions(undefined, this.activeOnlyFilter()).subscribe({
      next: (data) => {
        this.admissions.set(data || []);
        const transferId = this.selectedAdmissionForTransfer()?.id;
        const dischargeId = this.selectedAdmissionForDischarge()?.id;
        if (transferId) this.selectedAdmissionForTransfer.set(data?.find(a => a.id === transferId) || null);
        if (dischargeId) this.selectedAdmissionForDischarge.set(data?.find(a => a.id === dischargeId) || null);
      },
    });
  }

  loadPatients(): void {
    this.patientService.getPatients().subscribe({
      next: (data) => {
        this.availablePatients.set(data || []);
      },
    });
  }

  loadStaff(): void {
    this.staffService.getStaff().subscribe({
      next: (staff) => {
        this.staffList.set(staff || []);
      },
    });
  }

  setActiveTab(tab: 'grid' | 'admissions'): void {
    this.activeTab.set(tab);
  }

  onKpiClick(type: 'occupied' | 'available' | 'activeInpatients'): void {
    if (type === 'occupied') {
      this.activeTab.set('grid');
      this.selectedStatus.set('Occupied');
      this.selectedWard.set('');
      this.searchQuery.set('');
    } else if (type === 'available') {
      this.activeTab.set('grid');
      this.selectedStatus.set('Available');
      this.selectedWard.set('');
      this.searchQuery.set('');
    } else if (type === 'activeInpatients') {
      this.activeTab.set('admissions');
      this.activeOnlyFilter.set(true);
      this.loadAdmissions();
    }
  }

  onWardFilterChange(ward: string): void {
    this.selectedWard.set(ward);
  }

  clearGridFilters(): void {
    this.searchQuery.set('');
    this.selectedWard.set('');
    this.selectedStatus.set('');
  }

  toggleActiveOnlyFilter(): void {
    this.activeOnlyFilter.update((v) => !v);
    this.admCurrentPage.set(1);
    this.loadAdmissions();
  }

  goToAdmPage(page: number): void {
    if (page >= 1 && page <= this.admTotalPages()) {
      this.admCurrentPage.set(page);
    }
  }

  onBackdropClick(event: MouseEvent, dialogType: 'addBed' | 'admit' | 'transfer' | 'discharge'): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      if (dialogType === 'addBed') this.showAddBedDialog.set(false);
      if (dialogType === 'admit') this.showAdmitDialog.set(false);
      if (dialogType === 'transfer') this.showTransferDialog.set(false);
      if (dialogType === 'discharge') this.showDischargeDialog.set(false);
      document.body.style.overflow = '';
    }
  }

  // Modal 1: Add Bed
  openAddBedModal(): void {
    this.addBedError.set(null);
    this.bedForm.reset({
      wardName: 'General Ward A',
      roomNumber: '201',
      bedNumber: 'Bed-01',
      bedType: 'Standard Hospital Bed',
      dailyRate: 150,
    });
    this.showAddBedDialog.set(true);
    document.body.style.overflow = 'hidden';
  }

  submitAddBed(): void {
    if (this.bedForm.invalid) return;

    this.submitting.set(true);
    this.addBedError.set(null);
    const val = this.bedForm.value;

    this.inpatientService
      .createBed({
        wardName: val.wardName!.trim(),
        roomNumber: val.roomNumber!.trim(),
        bedNumber: val.bedNumber!.trim(),
        bedType: val.bedType!.trim(),
        dailyRate: Number(val.dailyRate),
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.showAddBedDialog.set(false);
          document.body.style.overflow = '';
          this.loadBeds();

          Swal.fire({
            title: 'Bed Created',
            text: `Bed ${val.bedNumber} in ${val.wardName} created successfully.`,
            icon: 'success',
            confirmButtonColor: '#0F766E',
            timer: 2200,
            timerProgressBar: true,
          });
        },
        error: (err) => {
          this.submitting.set(false);
          const msg = err.error?.detail || err.error?.message || 'Failed to create hospital bed.';
          this.addBedError.set(msg);
        },
      });
  }

  // Modal 2: Admit Patient
  openAdmitModal(): void {
    this.admitError.set(null);
    const firstPatient = this.availablePatients().length > 0 ? this.availablePatients()[0].id : '';
    const firstAvailableBed = this.availableBedsList().length > 0 ? this.availableBedsList()[0].id : '';
    const defaultDoctor = this.staffList().length > 0 ? `Dr. ${this.staffList()[0].firstName} ${this.staffList()[0].lastName}` : 'Dr. Sarah Connor';

    this.admitForm.reset({
      patientId: firstPatient,
      bedId: firstAvailableBed,
      admittingDoctorName: defaultDoctor,
      chiefComplaint: '',
    });
    this.showAdmitDialog.set(true);
    document.body.style.overflow = 'hidden';
  }

  openAdmitModalForBed(bed: Bed): void {
    this.admitError.set(null);
    const firstPatient = this.availablePatients().length > 0 ? this.availablePatients()[0].id : '';
    const defaultDoctor = this.staffList().length > 0 ? `Dr. ${this.staffList()[0].firstName} ${this.staffList()[0].lastName}` : 'Dr. Sarah Connor';

    this.admitForm.reset({
      patientId: firstPatient,
      bedId: bed.id,
      admittingDoctorName: defaultDoctor,
      chiefComplaint: '',
    });
    this.showAdmitDialog.set(true);
    document.body.style.overflow = 'hidden';
  }

  submitAdmitPatient(): void {
    if (this.admitForm.invalid || this.submitting()) return;

    this.submitting.set(true);
    this.admitError.set(null);
    const val = this.admitForm.value;

    this.inpatientService
      .admitPatient({
        patientId: val.patientId!,
        bedId: val.bedId!,
        admittingDoctorName: val.admittingDoctorName!.replace(/^Dr\.\s*/i, ''),
        chiefComplaint: val.chiefComplaint!.trim(),
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.showAdmitDialog.set(false);
          document.body.style.overflow = '';
          this.loadBeds();
          this.loadAdmissions();

          Swal.fire({
            title: 'Patient Admitted',
            text: 'Patient successfully assigned to hospital bed.',
            icon: 'success',
            confirmButtonColor: '#0F766E',
            timer: 2200,
            timerProgressBar: true,
          });
        },
        error: (err) => {
          this.submitting.set(false);
          const msg = err.error?.detail || err.error?.message || 'Failed to admit patient to bed.';
          this.admitError.set(msg);
          this.loadBeds();
          this.loadAdmissions();
        },
      });
  }

  // Modal 3: Discharge Patient
  openDischargeModalForBed(bed: Bed): void {
    let adm = this.admissions().find((a) => a.bedId === bed.id && !a.isDischarged);
    if (!adm && bed.currentAdmissionId) {
      adm = this.admissions().find((a) => a.id === bed.currentAdmissionId);
    }

    if (adm) {
      this.selectedAdmissionForDischarge.set(adm);
      this.dischargeForm.reset({
        dischargeSummary: 'Patient stabilized and discharged home with follow-up instructions.',
      });
      this.showDischargeDialog.set(true);
      document.body.style.overflow = 'hidden';
    } else {
      Swal.fire({
        title: 'Notice',
        text: 'Could not locate active admission for this bed.',
        icon: 'info',
        confirmButtonColor: '#0F766E',
      });
    }
  }

  openDischargeModalForAdmission(adm: Admission): void {
    this.selectedAdmissionForDischarge.set(adm);
    this.dischargeForm.reset({
      dischargeSummary: 'Patient stabilized and discharged home with follow-up instructions.',
    });
    this.showDischargeDialog.set(true);
    document.body.style.overflow = 'hidden';
  }

  submitDischargePatient(): void {
    const adm = this.selectedAdmissionForDischarge();
    if (!adm || this.dischargeForm.invalid || this.submitting()) return;

    this.submitting.set(true);
    const summary = this.dischargeForm.value.dischargeSummary!.trim();

    this.inpatientService.dischargePatient(adm.id, { dischargeSummary: summary }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.showDischargeDialog.set(false);
        this.selectedAdmissionForDischarge.set(null);
        document.body.style.overflow = '';
        this.loadBeds();
        this.loadAdmissions();

        Swal.fire({
          title: 'Patient Discharged',
          text: 'Inpatient discharge processed and hospital bed released.',
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2200,
          timerProgressBar: true,
        });
      },
      error: (err) => {
        this.submitting.set(false);
        Swal.fire({
          title: 'Error',
          text: err.error?.detail || err.error?.message || 'Failed to discharge patient.',
          icon: 'error',
          confirmButtonColor: '#E11D48',
        });
        this.loadBeds();
        this.loadAdmissions();
      },
    });
  }

  // Modal 4: Transfer Bed
  openTransferModalForBed(bed: Bed): void {
    let adm = this.admissions().find((a) => a.bedId === bed.id && !a.isDischarged);
    if (!adm && bed.currentAdmissionId) {
      adm = this.admissions().find((a) => a.id === bed.currentAdmissionId);
    }

    if (adm) {
      this.selectedAdmissionForTransfer.set(adm);
      const firstAvailable = this.availableBedsList().length > 0 ? this.availableBedsList()[0].id : '';

      this.transferForm.reset({
        newBedId: firstAvailable,
        transferReason: 'Transferred for specialized care monitoring.',
      });
      this.showTransferDialog.set(true);
      document.body.style.overflow = 'hidden';
    } else {
      Swal.fire({
        title: 'Notice',
        text: 'Could not locate active admission for this bed.',
        icon: 'info',
        confirmButtonColor: '#0F766E',
      });
    }
  }

  submitTransferPatient(): void {
    const adm = this.selectedAdmissionForTransfer();
    if (!adm || this.transferForm.invalid || this.submitting()) return;

    this.submitting.set(true);
    const val = this.transferForm.value;

    this.inpatientService
      .transferBed(adm.id, {
        newBedId: val.newBedId!,
        transferReason: val.transferReason?.trim() || undefined,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.showTransferDialog.set(false);
          this.selectedAdmissionForTransfer.set(null);
          document.body.style.overflow = '';
          this.loadBeds();
          this.loadAdmissions();

          Swal.fire({
            title: 'Patient Transferred',
            text: 'Patient successfully reassigned to new hospital bed.',
            icon: 'success',
            confirmButtonColor: '#0F766E',
            timer: 2200,
            timerProgressBar: true,
          });
        },
        error: (err) => {
          this.submitting.set(false);
          Swal.fire({
            title: 'Error',
            text: err.error?.detail || err.error?.message || 'Failed to transfer patient.',
            icon: 'error',
            confirmButtonColor: '#E11D48',
          });
          this.loadBeds();
          this.loadAdmissions();
        },
      });
  }

  // Format Helpers
  getInitials(name: string): string {
    if (!name) return 'PT';
    const parts = name.trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
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
}
