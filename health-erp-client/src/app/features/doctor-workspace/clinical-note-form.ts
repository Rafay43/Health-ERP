import { Component, inject, input, OnDestroy, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { DoctorWorkspaceService } from './data-access/doctor-workspace.service';

export const bloodPressureValidator: ValidatorFn = control => {
  const value = String(control.value ?? '').trim();
  if (!value) return null;
  const match = /^([0-9]{2,3})\/([0-9]{2,3})$/.exec(value);
  return match && +match[1] >= 40 && +match[1] <= 300 && +match[2] >= 20 && +match[2] <= 200 && +match[1] > +match[2] ? null : { bloodPressure: true };
};
const twoDecimals: ValidatorFn = control => control.value === null || /^\d+(?:\.\d{1,2})?$/.test(String(control.value)) ? null : { precision: true };
const meaningful: ValidatorFn = group => Object.values(group.value).some(value => typeof value === 'string' ? value.trim().length > 0 : value !== null) ? null : { empty: true };

@Component({
  selector: 'app-doctor-clinical-note-form', standalone: true, imports: [ReactiveFormsModule],
  templateUrl: './clinical-note-form.html',
})
export class DoctorClinicalNoteForm implements OnDestroy {
  readonly patientId = input.required<string>();
  readonly savingChange = output<boolean>();
  readonly saved = output<void>();
  readonly cancelled = output<void>();
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  private readonly api = inject(DoctorWorkspaceService);
  private request?: Subscription;
  readonly form = new FormGroup({
    chiefComplaint: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
    diagnosis: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    bloodPressure: new FormControl('', { nonNullable: true, validators: [bloodPressureValidator] }),
    heartRateBpm: new FormControl<number | null>(null, [Validators.min(1), Validators.max(300), control => control.value === null || Number.isInteger(control.value) ? null : { integer: true }]),
    temperatureCelsius: new FormControl<number | null>(null, [Validators.min(25), Validators.max(45), twoDecimals]),
    weightKg: new FormControl<number | null>(null, [control => control.value === null || control.value > 0 ? null : { positive: true }, Validators.max(500), twoDecimals]),
  }, { validators: meaningful });

  ngOnDestroy() { this.request?.unsubscribe(); }
  cancel(): void { if (!this.saving()) this.cancelled.emit(); }
  submit(): void {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const patientId = this.patientId(); // Immutable resource binding for this POST.
    this.saving.set(true); this.savingChange.emit(true); this.error.set(null);
    this.request = this.api.recordClinicalNote(patientId, {
      ...value, chiefComplaint: value.chiefComplaint.trim() || null, diagnosis: value.diagnosis.trim() || null,
      notes: value.notes.trim() || null, bloodPressure: value.bloodPressure.trim() || null,
    }).subscribe({
      next: () => { this.form.reset(); this.saving.set(false); this.savingChange.emit(false); this.saved.emit(); },
      error: error => {
        this.saving.set(false); this.savingChange.emit(false);
        this.error.set(error.status === 400 ? 'The note was not saved. Check the field limits, vital ranges and formats, then try again.'
          : error.status === 401 ? 'Your session has expired. Please sign in again.'
          : error.status === 403 ? 'Clinical note access is not available for this account.'
          : error.status === 404 ? 'Patient is not available.'
          : error.status === 409 ? 'The note could not be saved because the record changed. Please review and retry.'
          : 'Unable to confirm that the note was saved. Your entries have been kept. Refresh the snapshot before retrying.');
      },
    });
  }
}
