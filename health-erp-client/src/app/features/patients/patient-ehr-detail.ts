import { Component, inject, OnInit, signal, computed, HostListener } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { EhrService } from './data-access/ehr.service';
import { PatientEhr } from './data-access/ehr.model';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-patient-ehr-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    ReactiveFormsModule,
    DatePipe,
  ],
  templateUrl: './patient-ehr-detail.html',
})
export class PatientEhrDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private ehrService = inject(EhrService);
  private fb = inject(FormBuilder);

  ehr = signal<PatientEhr | null>(null);
  loading = signal<boolean>(false);
  submitting = signal<boolean>(false);
  formSubmitted = signal<boolean>(false);
  errorMessage = signal<string | null>(null);

  eventTypeFilter = signal<string | null>(null);
  showEncounterModal = signal<boolean>(false);

  addEncounterForm = this.fb.group({
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

    let list = [...data.timeline];
    const filter = this.eventTypeFilter();
    if (filter) {
      list = list.filter(t => t.eventType.toLowerCase() === filter.toLowerCase());
    }

    // Sort chronologically: NEWEST -> OLDEST
    list.sort((a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime());

    return list;
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

  getInitials(firstName?: string, lastName?: string): string {
    const f = firstName ? firstName.trim().charAt(0).toUpperCase() : '';
    const l = lastName ? lastName.trim().charAt(0).toUpperCase() : '';
    return `${f}${l}` || 'PT';
  }

  calculateAge(dobString?: string): string {
    if (!dobString) return '';
    const dob = new Date(dobString);
    if (isNaN(dob.getTime())) return '';
    const today = new Date();
    let age = today.getFullYear() - dob.getFullYear();
    const monthDiff = today.getMonth() - dob.getMonth();
    if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < dob.getDate())) {
      age--;
    }
    return age >= 0 ? `${age} years` : '';
  }

  openAddEncounterModal(): void {
    this.errorMessage.set(null);
    this.submitting.set(false);
    this.formSubmitted.set(false);
    this.addEncounterForm.reset({
      doctorName: 'Sarah Jenkins',
      chiefComplaint: '',
      diagnosis: '',
      notes: '',
      bloodPressure: '120/80',
      heartRateBpm: 72,
      temperatureCelsius: 37.0,
      weightKg: 70.0
    });
    this.showEncounterModal.set(true);
    if (typeof document !== 'undefined') {
      document.body.classList.add('overflow-hidden');
    }
  }

  closeAddEncounterModal(): void {
    this.showEncounterModal.set(false);
    this.errorMessage.set(null);
    this.submitting.set(false);
    this.formSubmitted.set(false);
    if (typeof document !== 'undefined') {
      document.body.classList.remove('overflow-hidden');
    }
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      if (!this.addEncounterForm.dirty || this.submitting()) {
        this.closeAddEncounterModal();
      }
    }
  }

  @HostListener('window:keydown.escape')
  onKeydownHandler(): void {
    if (this.showEncounterModal() && !this.submitting()) {
      this.closeAddEncounterModal();
    }
  }

  saveClinicalEncounter(): void {
    this.formSubmitted.set(true);
    const patient = this.ehr();
    if (!patient || this.addEncounterForm.invalid) {
      this.addEncounterForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);
    const val = this.addEncounterForm.value;

    const req = {
      doctorName: val.doctorName!,
      chiefComplaint: val.chiefComplaint!,
      diagnosis: val.diagnosis!,
      notes: val.notes || '',
      bloodPressure: val.bloodPressure || undefined,
      heartRateBpm: val.heartRateBpm ? Number(val.heartRateBpm) : undefined,
      temperatureCelsius: val.temperatureCelsius ? Number(val.temperatureCelsius) : undefined,
      weightKg: val.weightKg ? Number(val.weightKg) : undefined
    };

    this.ehrService.addClinicalNote(patient.patientId, req).subscribe({
      next: () => {
        this.closeAddEncounterModal();
        this.submitting.set(false);
        Swal.fire({
          title: 'Encounter Recorded!',
          text: 'Clinical encounter and patient vitals saved to EHR successfully.',
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2500,
          timerProgressBar: true
        });
        // Refresh patient timeline & vitals
        this.loadEhr(patient.patientId);
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set('Unable to save clinical encounter. Please verify entered details and try again.');
      }
    });
  }
}
