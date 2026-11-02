import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

import { AppointmentService } from '../data-access/appointment.service';
import { PatientService } from '../../patients/data-access/patient.service';
import { StaffService } from '../../staff/data-access/staff.service';
import { Patient } from '../../patients/data-access/patient.model';
import { Staff } from '../../staff/data-access/staff.model';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-book-appointment',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    CardModule,
    InputTextModule,
    ButtonModule,
    MessageModule,
  ],
  template: `
    <div class="max-w-2xl mx-auto">
      <div class="flex items-center gap-2 mb-4">
        <a routerLink="/appointments" class="text-[var(--color-text-secondary)] hover:text-[var(--color-primary)] flex items-center gap-1 text-sm font-medium">
          <i class="pi pi-arrow-left"></i> Back to Appointments
        </a>
      </div>

      <p-card header="Book New Appointment">
        <form [formGroup]="appointmentForm" (ngSubmit)="onSubmit()" class="flex flex-col gap-4 mt-2">
          <div *ngIf="errorMessage()" class="mb-2">
            <p-message severity="error">{{ errorMessage()! }}</p-message>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-1">
              <label for="patientId" class="text-sm font-medium text-[var(--color-text-primary)]">Patient *</label>
              <select
                id="patientId"
                formControlName="patientId"
                class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text-primary)] rounded p-2 text-sm focus:outline-none focus:border-[var(--color-primary)]"
              >
                <option value="">Select Patient</option>
                <option *ngFor="let p of patients()" [value]="p.id">
                  {{ p.firstName }} {{ p.lastName }} (MRN: {{ p.medicalRecordNumber }})
                </option>
              </select>
            </div>

            <div class="flex flex-col gap-1">
              <label for="staffId" class="text-sm font-medium text-[var(--color-text-primary)]">Doctor / Staff *</label>
              <select
                id="staffId"
                formControlName="staffId"
                class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text-primary)] rounded p-2 text-sm focus:outline-none focus:border-[var(--color-primary)]"
              >
                <option value="">Select Doctor</option>
                <option *ngFor="let s of staffList()" [value]="s.id">
                  Dr. {{ s.firstName }} {{ s.lastName }} ({{ s.specialty }})
                </option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-1">
              <label for="startDateTime" class="text-sm font-medium text-[var(--color-text-primary)]">Start Date & Time *</label>
              <input
                id="startDateTime"
                type="datetime-local"
                pInputText
                formControlName="startDateTime"
              />
            </div>
            <div class="flex flex-col gap-1">
              <label for="endDateTime" class="text-sm font-medium text-[var(--color-text-primary)]">End Date & Time *</label>
              <input
                id="endDateTime"
                type="datetime-local"
                pInputText
                formControlName="endDateTime"
              />
            </div>
          </div>

          <div class="flex flex-col gap-1">
            <label for="reason" class="text-sm font-medium text-[var(--color-text-primary)]">Reason for Visit *</label>
            <input
              id="reason"
              pInputText
              formControlName="reason"
              placeholder="e.g. Annual Checkup, Cardiac Consultation"
            />
          </div>

          <div class="flex flex-col gap-1">
            <label for="notes" class="text-sm font-medium text-[var(--color-text-primary)]">Additional Notes</label>
            <textarea
              id="notes"
              formControlName="notes"
              rows="3"
              class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text-primary)] rounded p-2 text-sm focus:outline-none focus:border-[var(--color-primary)]"
              placeholder="e.g. Patient requested morning slot"
            ></textarea>
          </div>

          <div class="flex justify-end gap-2 mt-4">
            <p-button label="Cancel" icon="pi pi-times" [text]="true" routerLink="/appointments"></p-button>
            <p-button
              type="submit"
              label="Schedule Appointment"
              icon="pi pi-calendar-plus"
              [loading]="isLoading()"
              [disabled]="appointmentForm.invalid || isLoading()"
            ></p-button>
          </div>
        </form>
      </p-card>
    </div>
  `,
})
export class BookAppointment implements OnInit {
  private fb = inject(FormBuilder);
  private appointmentService = inject(AppointmentService);
  private patientService = inject(PatientService);
  private staffService = inject(StaffService);
  private router = inject(Router);

  patients = signal<Patient[]>([]);
  staffList = signal<Staff[]>([]);
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  appointmentForm = this.fb.group({
    patientId: ['', [Validators.required]],
    staffId: ['', [Validators.required]],
    startDateTime: ['', [Validators.required]],
    endDateTime: ['', [Validators.required]],
    reason: ['', [Validators.required]],
    notes: [''],
  });

  ngOnInit(): void {
    this.patientService.getPatients().subscribe({
      next: (res) => this.patients.set(res),
      error: () => console.error('Failed to fetch patients'),
    });

    this.staffService.getStaff().subscribe({
      next: (res) => this.staffList.set(res),
      error: () => console.error('Failed to fetch staff members'),
    });
  }

  onSubmit(): void {
    if (this.appointmentForm.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const val = this.appointmentForm.value;

    const startUtc = new Date(val.startDateTime!).toISOString();
    const endUtc = new Date(val.endDateTime!).toISOString();

    this.appointmentService
      .bookAppointment({
        patientId: val.patientId!,
        staffId: val.staffId!,
        startDateTimeUtc: startUtc,
        endDateTimeUtc: endUtc,
        reason: val.reason!,
        notes: val.notes || undefined,
      })
      .subscribe({
        next: () => {
          this.isLoading.set(false);
          Swal.fire({
            title: 'Successful!',
            text: 'Appointment scheduled successfully.',
            icon: 'success',
            confirmButtonColor: '#10b981',
            timer: 2500,
            timerProgressBar: true
          });
          this.router.navigate(['/appointments']);
        },
        error: (err) => {
          this.isLoading.set(false);
          this.errorMessage.set(err.error?.detail || err.error?.message || 'Failed to schedule appointment.');
        },
      });
  }
}
