import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { PatientService } from './data-access/patient.service';
import { Patient } from './data-access/patient.model';

@Component({
  selector: 'app-patient-list',
  standalone: true,
  imports: [TableModule, ButtonModule, RouterLink],
  templateUrl: './patient-list.html',
})
export class PatientList implements OnInit {
  private patientService = inject(PatientService);

  patients = signal<Patient[]>([]);
  loading = signal(true);

  ngOnInit() {
    this.patientService.getPatients().subscribe({
      next: (data) => {
        this.patients.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }
}