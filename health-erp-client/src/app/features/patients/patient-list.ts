import { Component, inject, signal, computed, OnInit, HostListener } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PatientService } from './data-access/patient.service';
import { Patient } from './data-access/patient.model';
import { AuthService } from '../../core/services/auth.service';
import { Roles } from '../../core/constants/roles';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-patient-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, ReactiveFormsModule, DatePipe],
  templateUrl: './patient-list.html',
})
export class PatientList implements OnInit {
  private patientService = inject(PatientService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);

  canEditPatients = computed(() => this.authService.hasAnyRole([Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist]));

  // Table Data & Filters
  patients = signal<Patient[]>([]);
  loading = signal(true);
  searchTerm = signal('');
  sortBy = signal<'newest' | 'name-asc' | 'name-desc' | 'mrn'>('newest');
  currentPage = signal(1);
  pageSize = signal(10);

  // Supported Gender Options
  genderOptions = ['Male', 'Female', 'Other', 'Prefer not to say'];

  // Registration Modal State
  showRegisterModal = signal(false);
  submitting = signal(false);
  formSubmitted = signal(false);
  loadingMrn = signal(false);
  errorMessage = signal<string | null>(null);

  // Edit Modal State
  showEditModal = signal(false);
  editingPatient = signal<Patient | null>(null);
  editSubmitting = signal(false);
  editFormSubmitted = signal(false);
  editErrorMessage = signal<string | null>(null);

  // Registration Reactive Form
  registerForm = this.fb.group({
    medicalRecordNumber: [{ value: '', disabled: true }, [Validators.required]],
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    dateOfBirth: ['', [Validators.required]],
    gender: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    phoneNumber: ['', [Validators.maxLength(50)]],
  });

  // Edit Reactive Form
  editForm = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    dateOfBirth: ['', [Validators.required]],
    gender: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    phoneNumber: ['', [Validators.maxLength(50)]],
  });

  ngOnInit() {
    this.fetchPatients();
  }

  fetchPatients() {
    this.loading.set(true);
    this.patientService.getPatients().subscribe({
      next: (data) => {
        this.patients.set(data || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  // Search & Filter Logic
  filteredPatients = computed(() => {
    let list = [...this.patients()];
    const term = this.searchTerm().trim().toLowerCase();
    
    if (term) {
      list = list.filter(p =>
        (p.firstName && p.firstName.toLowerCase().includes(term)) ||
        (p.lastName && p.lastName.toLowerCase().includes(term)) ||
        (p.medicalRecordNumber && p.medicalRecordNumber.toLowerCase().includes(term)) ||
        (p.email && p.email.toLowerCase().includes(term)) ||
        (`${p.firstName} ${p.lastName}`.toLowerCase().includes(term))
      );
    }

    const sort = this.sortBy();
    if (sort === 'name-asc') {
      list.sort((a, b) => `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`));
    } else if (sort === 'name-desc') {
      list.sort((a, b) => `${b.firstName} ${b.lastName}`.localeCompare(`${a.firstName} ${a.lastName}`));
    } else if (sort === 'mrn') {
      list.sort((a, b) => (a.medicalRecordNumber || '').localeCompare(b.medicalRecordNumber || ''));
    }

    return list;
  });

  totalPages = computed(() => {
    return Math.ceil(this.filteredPatients().length / this.pageSize()) || 1;
  });

  paginatedPatients = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize();
    return this.filteredPatients().slice(start, start + this.pageSize());
  });

  // Initials Avatar Generator
  getInitials(firstName: string, lastName: string): string {
    const f = firstName ? firstName.trim().charAt(0).toUpperCase() : '';
    const l = lastName ? lastName.trim().charAt(0).toUpperCase() : '';
    return `${f}${l}` || 'PT';
  }

  getAvatarColor(name: string): string {
    const colors = [
      'bg-teal-50 text-[#0F766E] border-teal-200/60',
      'bg-sky-50 text-[#0284C7] border-sky-200/60',
      'bg-indigo-50 text-[#4F46E5] border-indigo-200/60',
      'bg-emerald-50 text-[#059669] border-emerald-200/60',
      'bg-amber-50 text-[#D97706] border-amber-200/60',
      'bg-violet-50 text-[#7C3AED] border-violet-200/60'
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    const index = Math.abs(hash) % colors.length;
    return colors[index];
  }

  calculateAge(dobString: string): string {
    if (!dobString) return 'N/A';
    const dob = new Date(dobString);
    if (isNaN(dob.getTime())) return 'N/A';
    
    const today = new Date();
    let age = today.getFullYear() - dob.getFullYear();
    const monthDiff = today.getMonth() - dob.getMonth();
    
    if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < dob.getDate())) {
      age--;
    }
    
    return age >= 0 ? `${age} yrs` : 'N/A';
  }

  get maxDobDate(): string {
    const today = new Date();
    const y = today.getFullYear();
    const m = String(today.getMonth() + 1).padStart(2, '0');
    const d = String(today.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  // Modal Handlers - Register
  openRegisterModal() {
    this.errorMessage.set(null);
    this.submitting.set(false);
    this.formSubmitted.set(false);
    this.registerForm.reset();
    this.loadingMrn.set(true);
    this.showRegisterModal.set(true);

    if (typeof document !== 'undefined') {
      document.body.classList.add('overflow-hidden');
    }

    this.patientService.getNextMrn().subscribe({
      next: (res) => {
        this.registerForm.patchValue({ medicalRecordNumber: res.medicalRecordNumber });
        this.loadingMrn.set(false);
      },
      error: () => {
        this.loadingMrn.set(false);
      },
    });
  }

  closeRegisterModal() {
    this.showRegisterModal.set(false);
    this.errorMessage.set(null);
    this.submitting.set(false);
    this.formSubmitted.set(false);
    if (typeof document !== 'undefined') {
      document.body.classList.remove('overflow-hidden');
    }
  }

  // Modal Handlers - Edit
  openEditModal(patient: Patient, event?: MouseEvent) {
    if (event) event.stopPropagation();
    this.editingPatient.set(patient);
    this.editErrorMessage.set(null);
    this.editSubmitting.set(false);
    this.editFormSubmitted.set(false);

    let formattedDob = '';
    if (patient.dateOfBirth) {
      const d = new Date(patient.dateOfBirth);
      if (!isNaN(d.getTime())) {
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        formattedDob = `${y}-${m}-${day}`;
      } else {
        formattedDob = patient.dateOfBirth.substring(0, 10);
      }
    }

    this.editForm.setValue({
      firstName: patient.firstName || '',
      lastName: patient.lastName || '',
      dateOfBirth: formattedDob,
      gender: patient.gender || '',
      email: patient.email || '',
      phoneNumber: patient.phoneNumber || '',
    });

    this.showEditModal.set(true);

    if (typeof document !== 'undefined') {
      document.body.classList.add('overflow-hidden');
    }
  }

  closeEditModal() {
    this.showEditModal.set(false);
    this.editingPatient.set(null);
    this.editErrorMessage.set(null);
    this.editSubmitting.set(false);
    this.editFormSubmitted.set(false);
    if (typeof document !== 'undefined') {
      document.body.classList.remove('overflow-hidden');
    }
  }

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      if (!this.registerForm.dirty || this.submitting()) {
        this.closeRegisterModal();
      }
      if (!this.editForm.dirty || this.editSubmitting()) {
        this.closeEditModal();
      }
    }
  }

  @HostListener('window:keydown.escape')
  onKeydownHandler() {
    if (this.showRegisterModal() && !this.submitting()) {
      this.closeRegisterModal();
    }
    if (this.showEditModal() && !this.editSubmitting()) {
      this.closeEditModal();
    }
  }

  onRegisterSubmit() {
    this.formSubmitted.set(true);

    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const raw = this.registerForm.getRawValue();
    const dobVal = raw.dateOfBirth as any;
    let dateOfBirthString = '';
    
    if (typeof dobVal === 'string') {
      dateOfBirthString = dobVal;
    } else if (dobVal && typeof dobVal === 'object' && 'getFullYear' in dobVal) {
      const y = dobVal.getFullYear();
      const m = String(dobVal.getMonth() + 1).padStart(2, '0');
      const d = String(dobVal.getDate()).padStart(2, '0');
      dateOfBirthString = `${y}-${m}-${d}`;
    }

    this.patientService
      .registerPatient({
        medicalRecordNumber: raw.medicalRecordNumber!,
        firstName: raw.firstName!,
        lastName: raw.lastName!,
        dateOfBirth: dateOfBirthString,
        gender: raw.gender!,
        email: raw.email!,
        phoneNumber: raw.phoneNumber || undefined,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.closeRegisterModal();
          this.fetchPatients();
          
          Swal.fire({
            title: 'Patient Registered!',
            text: `Patient ${raw.firstName} ${raw.lastName} successfully registered with MRN ${raw.medicalRecordNumber}.`,
            icon: 'success',
            confirmButtonColor: '#0F766E',
            timer: 2500,
            timerProgressBar: true
          });
        },
        error: (err) => {
          this.submitting.set(false);
          if (err.status === 409) {
            this.errorMessage.set('A patient with this medical record number already exists.');
          } else if (err.status === 400) {
            const detail = err.error?.detail || err.error?.title || 'Please check the form for errors and try again.';
            this.errorMessage.set(detail);
          } else {
            this.errorMessage.set('Unable to register patient. Please try again.');
          }
        },
      });
  }

  onEditSubmit() {
    this.editFormSubmitted.set(true);
    const patient = this.editingPatient();
    if (!patient || this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }

    this.editSubmitting.set(true);
    this.editErrorMessage.set(null);

    const raw = this.editForm.getRawValue();
    const dobVal = raw.dateOfBirth as any;
    let dateOfBirthString = '';
    
    if (typeof dobVal === 'string') {
      dateOfBirthString = dobVal;
    } else if (dobVal && typeof dobVal === 'object' && 'getFullYear' in dobVal) {
      const y = dobVal.getFullYear();
      const m = String(dobVal.getMonth() + 1).padStart(2, '0');
      const d = String(dobVal.getDate()).padStart(2, '0');
      dateOfBirthString = `${y}-${m}-${d}`;
    }

    const trimmedEmail = (raw.email || '').trim();

    this.patientService
      .updatePatient(patient.id, {
        firstName: raw.firstName!,
        lastName: raw.lastName!,
        dateOfBirth: dateOfBirthString,
        gender: raw.gender!,
        email: trimmedEmail,
        phoneNumber: raw.phoneNumber ? raw.phoneNumber.trim() : undefined,
      })
      .subscribe({
        next: () => {
          this.editSubmitting.set(false);
          this.closeEditModal();
          this.fetchPatients();

          Swal.fire({
            title: 'Patient Updated!',
            text: 'Patient information updated successfully.',
            icon: 'success',
            confirmButtonColor: '#0F766E',
            timer: 2500,
            timerProgressBar: true
          });
        },
        error: (err) => {
          this.editSubmitting.set(false);
          if (err.status === 400) {
            const detail = err.error?.detail || err.error?.title || 'Please check the form for errors and try again.';
            this.editErrorMessage.set(detail);
          } else {
            this.editErrorMessage.set('Unable to update patient. Please try again.');
          }
        },
      });
  }

  onSearchChange(term: string) {
    this.searchTerm.set(term);
    this.currentPage.set(1);
  }

  onSortChange(sort: any) {
    this.sortBy.set(sort);
    this.currentPage.set(1);
  }

  goToPage(page: number) {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  get Math() {
    return Math;
  }
}