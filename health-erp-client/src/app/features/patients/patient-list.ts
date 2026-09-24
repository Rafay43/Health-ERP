import { Component } from '@angular/core';

@Component({
  selector: 'app-patient-list',
  standalone: true,
  template: `
    <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Patients</h1>
    <p class="text-[var(--color-text-secondary)] mt-2">Patient list coming next.</p>
  `,
})
export class PatientList {}