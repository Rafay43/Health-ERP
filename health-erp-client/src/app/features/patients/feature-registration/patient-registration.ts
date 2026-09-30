import { Component, inject, signal, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { PatientService } from '../data-access/patient.service';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-patient-registration',
  standalone: true,
  imports: [ReactiveFormsModule, InputTextModule, DatePickerModule, ButtonModule, MessageModule],
  templateUrl: './patient-registration.html',
})
export class PatientRegistration implements OnInit {
  private fb = inject(FormBuilder);
  private patientService = inject(PatientService);
  private router = inject(Router);

  submitting = signal(false);
  errorMessage = signal<string | null>(null);
  loadingMrn = signal(true);

  form = this.fb.group({
    medicalRecordNumber: [{ value: '', disabled: true }, [Validators.required]],
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    dateOfBirth: [null as Date | null, [Validators.required]],
  });

  ngOnInit() {
    this.patientService.getNextMrn().subscribe({
      next: (res) => {
        this.form.patchValue({ medicalRecordNumber: res.medicalRecordNumber });
        this.loadingMrn.set(false);
      },
      error: () => {
        this.loadingMrn.set(false);
      },
    });
  }

  onSubmit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const dob = raw.dateOfBirth as Date;

    const year = dob.getFullYear();
    const month = String(dob.getMonth() + 1).padStart(2, '0');
    const day = String(dob.getDate()).padStart(2, '0');
    const dateOfBirthString = `${year}-${month}-${day}`;

    this.patientService
      .registerPatient({
        medicalRecordNumber: raw.medicalRecordNumber!,
        firstName: raw.firstName!,
        lastName: raw.lastName!,
        dateOfBirth: dateOfBirthString,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          Swal.fire({
            title: 'Successful!',
            text: `Patient ${raw.firstName} ${raw.lastName} registered successfully with MRN ${raw.medicalRecordNumber}.`,
            icon: 'success',
            confirmButtonColor: '#10b981',
            timer: 2500,
            timerProgressBar: true
          });
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