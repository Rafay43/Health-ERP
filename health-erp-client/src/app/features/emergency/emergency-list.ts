import { Component, inject, OnInit, signal, computed, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { EmergencyService } from './data-access/emergency.service';
import { TriageRecord } from './data-access/emergency.model';
import { PatientService } from '../patients/data-access/patient.service';
import { Patient } from '../patients/data-access/patient.model';
import { InpatientService } from '../inpatient/data-access/inpatient.service';
import { Bed } from '../inpatient/data-access/inpatient.model';
import { StaffService } from '../staff/data-access/staff.service';
import { Staff } from '../staff/data-access/staff.model';
import { AuthService } from '../../core/services/auth.service';
import { Roles } from '../../core/constants/roles';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-emergency-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './emergency-list.html',
})
export class EmergencyListComponent implements OnInit {
  private emergencyService = inject(EmergencyService);
  private patientService = inject(PatientService);
  private inpatientService = inject(InpatientService);
  private staffService = inject(StaffService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);

  get canIntake(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist]);
  }

  get canAssignBay(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist]);
  }

  get canTransferInpatient(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.Nurse, Roles.Receptionist]);
  }

  get canDischarge(): boolean {
    return this.authService.hasAnyRole([Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist]);
  }

  // Global Math for template expressions
  Math = Math;

  // State Signals
  triageRecords = signal<TriageRecord[]>([]);
  patientList = signal<Patient[]>([]);
  availableBeds = signal<Bed[]>([]);
  staffList = signal<Staff[]>([]);
  loading = signal<boolean>(false);
  submitting = signal<boolean>(false);

  // Navigation & Filters
  activeOnlyFilter = signal<boolean>(true);
  searchQuery = signal<string>('');
  selectedSeverity = signal<string>('');
  selectedStatus = signal<string>('');

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Dialog Visibility Flags
  showTriageDialog = signal<boolean>(false);
  showAssignBayDialog = signal<boolean>(false);
  showTransferDialog = signal<boolean>(false);

  selectedTriageForBay = signal<TriageRecord | null>(null);
  selectedTriageForTransfer = signal<TriageRecord | null>(null);

  // Feedback flags
  triageError = signal<string | null>(null);

  // Reactive Forms (UN-PREFILLED VITAL INPUTS FOR CLINICAL DATA ACCURACY)
  triageForm = this.fb.group({
    patientId: ['', Validators.required],
    severity: [2, Validators.required],
    chiefComplaint: ['', Validators.required],
    bloodPressure: [''],
    heartRateBpm: [null as number | null],
    oxygenSaturationSpO2: [null as number | null],
    bodyTemperatureCelsius: [null as number | null],
    respiratoryRateBpm: [null as number | null],
    triageNurseName: ['Nurse Clara Barton', Validators.required],
    assignedBay: [''],
  });

  assignBayForm = this.fb.group({
    bayName: ['', Validators.required],
    attendingPhysicianName: ['Dr. Gregory House', Validators.required],
  });

  transferForm = this.fb.group({
    bedId: ['', Validators.required],
    admittingDoctorName: ['Demo Clinician', Validators.required],
    admissionNotes: ['Direct ER transfer for inpatient care', Validators.required],
  });

  // Computed KPI Signals
  level1Count = computed(
    () =>
      this.triageRecords().filter(
        (r) => r.severityLevel === 1 && r.statusName !== 'DischargedFromED' && r.statusName !== 'TransferredToInpatient'
      ).length
  );

  level2Count = computed(
    () =>
      this.triageRecords().filter(
        (r) => r.severityLevel === 2 && r.statusName !== 'DischargedFromED' && r.statusName !== 'TransferredToInpatient'
      ).length
  );

  inBayCareCount = computed(
    () =>
      this.triageRecords().filter(
        (r) => r.statusName === 'InBayCare'
      ).length
  );

  activeQueueCount = computed(
    () =>
      this.triageRecords().filter(
        (r) => r.statusName !== 'DischargedFromED' && r.statusName !== 'TransferredToInpatient'
      ).length
  );

  historicalCount = computed(
    () =>
      this.triageRecords().filter(
        (r) => r.statusName === 'DischargedFromED' || r.statusName === 'TransferredToInpatient'
      ).length
  );

  // Filtered Queue List
  filteredTriageRecords = computed(() => {
    let list = this.triageRecords();

    // 1. Tab filter
    if (this.activeOnlyFilter()) {
      list = list.filter((r) => r.statusName !== 'DischargedFromED' && r.statusName !== 'TransferredToInpatient');
    } else {
      list = list.filter((r) => r.statusName === 'DischargedFromED' || r.statusName === 'TransferredToInpatient');
    }

    // 2. Search query
    const query = this.searchQuery().trim().toLowerCase();
    if (query) {
      list = list.filter(
        (r) =>
          r.patientName?.toLowerCase().includes(query) ||
          r.patientMrn?.toLowerCase().includes(query) ||
          r.chiefComplaint?.toLowerCase().includes(query) ||
          r.assignedBay?.toLowerCase().includes(query) ||
          r.triageNurseName?.toLowerCase().includes(query)
      );
    }

    // 3. Severity filter
    const sev = this.selectedSeverity();
    if (sev) {
      list = list.filter((r) => r.severityLevel === Number(sev));
    }

    // 4. Status filter
    const status = this.selectedStatus();
    if (status) {
      list = list.filter((r) => r.statusName.toLowerCase() === status.toLowerCase());
    }

    // 5. Clinical Priority Sorting for Active Queue (ESI 1 first, ESI 2 second, etc.)
    if (this.activeOnlyFilter()) {
      list = [...list].sort((a, b) => a.severityLevel - b.severityLevel);
    }

    return list;
  });

  // Paginated List
  paginatedTriageRecords = computed(() => {
    const list = this.filteredTriageRecords();
    const page = this.currentPage();
    const size = this.pageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  totalPages = computed(() => {
    const count = this.filteredTriageRecords().length;
    return Math.ceil(count / this.pageSize()) || 1;
  });

  ngOnInit(): void {
    this.loadEmergencyQueue();
    this.loadPatients();
    this.loadBeds();
    this.loadStaff();
  }

  @HostListener('window:keydown.escape')
  onEscapeKey(): void {
    if (this.showTriageDialog()) this.showTriageDialog.set(false);
    if (this.showAssignBayDialog()) this.showAssignBayDialog.set(false);
    if (this.showTransferDialog()) this.showTransferDialog.set(false);
    document.body.style.overflow = '';
  }

  loadEmergencyQueue(): void {
    this.loading.set(true);
    this.emergencyService.getEmergencyQueue(this.activeOnlyFilter()).subscribe({
      next: (data) => {
        this.triageRecords.set(data || []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  loadPatients(): void {
    this.patientService.getPatients().subscribe({
      next: (data) => this.patientList.set(data || []),
    });
  }

  loadBeds(): void {
    this.inpatientService.getBedGrid().subscribe({
      next: (data) => this.availableBeds.set((data || []).filter((b) => b.status === 'Available')),
    });
  }

  loadStaff(): void {
    this.staffService.getStaff().subscribe({
      next: (staff) => this.staffList.set(staff || []),
    });
  }

  toggleActiveFilter(active: boolean): void {
    this.activeOnlyFilter.set(active);
    this.currentPage.set(1);
    this.loadEmergencyQueue();
  }

  onKpiClick(type: 'esi1' | 'esi2' | 'inBay' | 'activeQueue'): void {
    this.activeOnlyFilter.set(true);
    this.currentPage.set(1);

    if (type === 'esi1') {
      this.selectedSeverity.set('1');
      this.selectedStatus.set('');
      this.searchQuery.set('');
    } else if (type === 'esi2') {
      this.selectedSeverity.set('2');
      this.selectedStatus.set('');
      this.searchQuery.set('');
    } else if (type === 'inBay') {
      this.selectedStatus.set('InBayCare');
      this.selectedSeverity.set('');
      this.searchQuery.set('');
    } else if (type === 'activeQueue') {
      this.selectedSeverity.set('');
      this.selectedStatus.set('');
      this.searchQuery.set('');
    }
  }

  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedSeverity.set('');
    this.selectedStatus.set('');
    this.currentPage.set(1);
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  onBackdropClick(event: MouseEvent, dialogType: 'triage' | 'assignBay' | 'transfer'): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      if (dialogType === 'triage') this.showTriageDialog.set(false);
      if (dialogType === 'assignBay') this.showAssignBayDialog.set(false);
      if (dialogType === 'transfer') this.showTransferDialog.set(false);
      document.body.style.overflow = '';
    }
  }

  // Modal 1: Rapid ER Intake
  openAddTriageModal(): void {
    this.triageError.set(null);
    const firstPatient = this.patientList().length > 0 ? this.patientList()[0].id : '';
    const defaultNurse = this.staffList().length > 0 ? `Nurse ${this.staffList()[0].firstName} ${this.staffList()[0].lastName}` : 'Nurse Clara Barton';

    this.triageForm.reset({
      patientId: firstPatient,
      severity: 2,
      chiefComplaint: '',
      bloodPressure: '',
      heartRateBpm: null,
      oxygenSaturationSpO2: null,
      bodyTemperatureCelsius: null,
      respiratoryRateBpm: null,
      triageNurseName: defaultNurse,
      assignedBay: '',
    });

    this.showTriageDialog.set(true);
    document.body.style.overflow = 'hidden';
  }

  submitTriage(): void {
    if (this.triageForm.invalid) return;

    this.submitting.set(true);
    this.triageError.set(null);
    const val = this.triageForm.value;

    const payload = {
      patientId: val.patientId!,
      chiefComplaint: val.chiefComplaint!.trim(),
      bloodPressure: val.bloodPressure?.trim() || '',
      heartRateBpm: val.heartRateBpm != null ? Number(val.heartRateBpm) : 0,
      oxygenSaturationSpO2: val.oxygenSaturationSpO2 != null ? Number(val.oxygenSaturationSpO2) : 0,
      bodyTemperatureCelsius: val.bodyTemperatureCelsius != null ? Number(val.bodyTemperatureCelsius) : 0,
      respiratoryRateBpm: val.respiratoryRateBpm != null ? Number(val.respiratoryRateBpm) : 0,
      severity: Number(val.severity),
      triageNurseName: val.triageNurseName!.trim(),
      assignedBay: val.assignedBay?.trim() || undefined,
    };

    this.emergencyService.createTriageRecord(payload).subscribe({
      next: () => {
        this.submitting.set(false);
        this.showTriageDialog.set(false);
        document.body.style.overflow = '';
        this.loadEmergencyQueue();

        Swal.fire({
          title: 'ER Triage Registered',
          text: 'Emergency patient has been added to the triage queue.',
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2200,
          timerProgressBar: true,
        });
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err.error?.detail || err.error?.message || 'Failed to register ER triage encounter.';
        this.triageError.set(msg);
      },
    });
  }

  // Modal 2: Assign Bay & Physician
  openAssignBayModal(rec: TriageRecord): void {
    this.selectedTriageForBay.set(rec);
    const defaultDoctor = this.staffList().length > 0 ? `Dr. ${this.staffList()[0].firstName} ${this.staffList()[0].lastName}` : 'Dr. Gregory House';

    this.assignBayForm.reset({
      bayName: rec.assignedBay || 'Trauma Bay 01',
      attendingPhysicianName: rec.attendingPhysicianName || defaultDoctor,
    });
    this.showAssignBayDialog.set(true);
    document.body.style.overflow = 'hidden';
  }

  submitAssignBay(): void {
    const rec = this.selectedTriageForBay();
    if (!rec || this.assignBayForm.invalid) return;

    this.submitting.set(true);
    const val = this.assignBayForm.value;

    this.emergencyService
      .assignBay(rec.id, {
        bayName: val.bayName!.trim(),
        attendingPhysicianName: val.attendingPhysicianName!.trim(),
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.showAssignBayDialog.set(false);
          this.selectedTriageForBay.set(null);
          document.body.style.overflow = '';
          this.loadEmergencyQueue();

          Swal.fire({
            title: 'Bay Assigned',
            text: `Patient assigned to ${val.bayName} successfully.`,
            icon: 'success',
            confirmButtonColor: '#0F766E',
            timer: 2200,
            timerProgressBar: true,
          });
        },
        error: () => this.submitting.set(false),
      });
  }

  // Modal 3: Transfer to Ward
  openTransferModal(rec: TriageRecord): void {
    this.selectedTriageForTransfer.set(rec);
    const firstBed = this.availableBeds().length > 0 ? this.availableBeds()[0].id : '';
    const doctor = rec.attendingPhysicianName || 'Dr. Sarah Connor';

    this.transferForm.reset({
      bedId: firstBed,
      admittingDoctorName: doctor,
      admissionNotes: `ER Transfer: ${rec.chiefComplaint}`,
    });
    this.showTransferDialog.set(true);
    document.body.style.overflow = 'hidden';
  }

  submitTransfer(): void {
    const rec = this.selectedTriageForTransfer();
    if (!rec || this.transferForm.invalid || this.submitting()) return;

    this.submitting.set(true);
    const val = this.transferForm.value;

    this.emergencyService
      .transferToInpatient(rec.id, {
        bedId: val.bedId!,
        admittingDoctorName: val.admittingDoctorName!.trim(),
        admissionNotes: val.admissionNotes!.trim(),
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.showTransferDialog.set(false);
          this.selectedTriageForTransfer.set(null);
          document.body.style.overflow = '';
          this.loadEmergencyQueue();
          this.loadBeds();

          Swal.fire({
            title: 'Transferred to Ward',
            text: 'Patient successfully transferred to inpatient ward bed.',
            icon: 'success',
            confirmButtonColor: '#0F766E',
            timer: 2200,
            timerProgressBar: true,
          });
        },
        error: (err) => {
          this.submitting.set(false);
          Swal.fire({ title: 'Transfer Failed',
            text: err.error?.detail || err.error?.message || 'Unable to confirm transfer. Refresh the queue and beds before retrying.',
            icon: 'error', confirmButtonColor: '#E11D48' });
          this.loadEmergencyQueue();
          this.loadBeds();
        },
      });
  }

  // Action: Discharge from ER
  dischargeFromER(rec: TriageRecord): void {
    Swal.fire({
      title: 'Discharge Patient?',
      text: `Are you sure you want to discharge ${rec.patientName} from Emergency Department?`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonColor: '#0F766E',
      cancelButtonColor: '#64748B',
      confirmButtonText: 'Yes, Discharge ER',
    }).then((result) => {
      if (result.isConfirmed) {
        this.submitting.set(true);
        this.emergencyService.dischargeFromEmergency(rec.id).subscribe({
          next: () => {
            this.submitting.set(false);
            this.loadEmergencyQueue();

            Swal.fire({
              title: 'Discharged',
              text: 'Patient discharged from Emergency Department.',
              icon: 'success',
              confirmButtonColor: '#0F766E',
              timer: 2200,
              timerProgressBar: true,
            });
          },
          error: () => this.submitting.set(false),
        });
      }
    });
  }

  // Formatting & Display Helpers
  getSeverityLabel(level: number): string {
    switch (level) {
      case 1:
        return 'Resuscitation';
      case 2:
        return 'Emergent';
      case 3:
        return 'Urgent';
      case 4:
        return 'Less Urgent';
      case 5:
        return 'Non-Urgent';
      default:
        return 'Unknown';
    }
  }

  getSeverityBadgeClass(level: number): string {
    switch (level) {
      case 1:
        return 'bg-rose-100 text-rose-800 border border-rose-300 font-extrabold';
      case 2:
        return 'bg-orange-100 text-orange-800 border border-orange-300 font-bold';
      case 3:
        return 'bg-amber-50 text-amber-800 border border-amber-200 font-bold';
      case 4:
        return 'bg-blue-50 text-blue-700 border border-blue-200';
      case 5:
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      default:
        return 'bg-slate-100 text-slate-700 border border-slate-200';
    }
  }

  getSeverityDotClass(level: number): string {
    switch (level) {
      case 1:
        return 'bg-rose-600 animate-ping';
      case 2:
        return 'bg-orange-600';
      case 3:
        return 'bg-amber-500';
      case 4:
        return 'bg-blue-500';
      case 5:
        return 'bg-emerald-500';
      default:
        return 'bg-slate-400';
    }
  }

  getSeverityRowBorderClass(level: number): string {
    switch (level) {
      case 1:
        return 'border-l-4 border-l-rose-500';
      case 2:
        return 'border-l-4 border-l-orange-500';
      case 3:
        return 'border-l-4 border-l-amber-400';
      default:
        return '';
    }
  }

  getSeveritySelectClass(level: any): string {
    const lvl = Number(level);
    switch (lvl) {
      case 1:
        return 'border-rose-500 bg-rose-50 text-rose-900';
      case 2:
        return 'border-orange-500 bg-orange-50 text-orange-900';
      case 3:
        return 'border-amber-400 bg-amber-50 text-amber-900';
      default:
        return 'border-slate-200 bg-slate-50 text-slate-800';
    }
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case 'WaitingForTriage':
        return 'Waiting Triage';
      case 'Triaged':
        return 'Triaged';
      case 'InBayCare':
        return 'In Bay Care';
      case 'TransferredToInpatient':
        return 'Transferred to Ward';
      case 'DischargedFromED':
        return 'Discharged ER';
      default:
        return status;
    }
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'InBayCare':
        return 'bg-blue-50 text-blue-700 border border-blue-200';
      case 'Triaged':
        return 'bg-amber-50 text-amber-700 border border-amber-200';
      case 'TransferredToInpatient':
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      case 'DischargedFromED':
        return 'bg-slate-100 text-slate-700 border border-slate-200';
      default:
        return 'bg-amber-50 text-amber-700 border border-amber-200';
    }
  }

  formatTime(isoString: string): string {
    if (!isoString) return '';
    const d = new Date(isoString);
    if (isNaN(d.getTime())) return isoString;
    return d.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });
  }

  calculateWaitTime(arrivalIso: string): string {
    if (!arrivalIso) return '-';
    const arrival = new Date(arrivalIso).getTime();
    if (isNaN(arrival)) return '-';

    const now = new Date().getTime();
    const diffMs = Math.max(0, now - arrival);
    const diffMins = Math.floor(diffMs / 60000);

    if (diffMins < 60) {
      return `${diffMins} min`;
    }
    const hrs = Math.floor(diffMins / 60);
    const mins = diffMins % 60;
    return `${hrs}h ${mins}m`;
  }
}
