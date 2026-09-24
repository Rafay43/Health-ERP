import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { PatientService } from '../data-access/patient.service';

@Component({
  selector: 'app-patient-registration',
  standalone: true,
  imports: [ReactiveFormsModule, InputTextModule, DatePickerModule, ButtonModule, MessageModule],
  templateUrl: './patient-registration.html',
})
export class PatientRegistration {
  private fb = inject(FormBuilder);
  private patientService = inject(PatientService);
  private router = inject(Router);

  submitting = signal(false);
  errorMessage = signal<string | null>(null);

  form = this.fb.group({
    medicalRecordNumber: ['', [Validators.required, Validators.maxLength(20)]],
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    dateOfBirth: [null as Date | null, [Validators.required]],
  });

  onSubmit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const dob = raw.dateOfBirth as Date;

    this.patientService
      .registerPatient({
        medicalRecordNumber: raw.medicalRecordNumber!,
        firstName: raw.firstName!,
        lastName: raw.lastName!,
        dateOfBirth: dob.toISOString().split('T')[0],
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.router.navigate(['/patients']);
        },
        error: (err) => {
          this.submitting.set(false);
          if (err.status === 409) {
            this.errorMessage.set('A patient with this medical record number already exists.');
          } else if (err.status === 400) {
            this.errorMessage.set('Please check the form for errors.');
          } else {
            this.errorMessage.set('Something went wrong. Please try again.');
          }
        },
      });
  }
}