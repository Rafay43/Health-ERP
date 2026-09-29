import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';

import { AppointmentService } from './data-access/appointment.service';
import { Appointment } from './data-access/appointment.model';

@Component({
  selector: 'app-appointment-list',
  standalone: true,
  imports: [CommonModule, RouterLink, TableModule, ButtonModule, TagModule],
  template: `
    <div class="flex flex-col gap-4">
      <div class="flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Appointments & Scheduling</h1>
          <p class="text-sm text-[var(--color-text-secondary)]">View scheduled patient consultations and doctor agendas</p>
        </div>
        <p-button
          label="Book Appointment"
          icon="pi pi-calendar-plus"
          routerLink="/appointments/book"
        ></p-button>
      </div>

      <div class="bg-[var(--color-surface)] rounded-lg border border-[var(--color-border)] p-4">
        <p-table [value]="appointments()" [loading]="isLoading()" [paginator]="true" [rows]="10">
          <ng-template pTemplate="header">
            <tr>
              <th>Patient</th>
              <th>Doctor / Staff</th>
              <th>Start Time</th>
              <th>End Time</th>
              <th>Reason</th>
              <th>Status</th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-apt>
            <tr>
              <td class="font-semibold text-[var(--color-text-primary)]">{{ apt.patientName }}</td>
              <td>{{ apt.doctorName }}</td>
              <td>{{ apt.startDateTimeUtc | date:'medium' }}</td>
              <td>{{ apt.endDateTimeUtc | date:'shortTime' }}</td>
              <td>{{ apt.reason }}</td>
              <td>
                <p-tag
                  [value]="apt.status"
                  [severity]="getStatusSeverity(apt.status)"
                ></p-tag>
              </td>
            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="6" class="text-center py-6 text-[var(--color-text-secondary)]">
                No appointments found. Click "Book Appointment" to schedule one.
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `,
})
export class AppointmentList implements OnInit {
  private appointmentService = inject(AppointmentService);

  appointments = signal<Appointment[]>([]);
  isLoading = signal(true);

  ngOnInit(): void {
    this.loadAppointments();
  }

  loadAppointments(): void {
    this.isLoading.set(true);
    this.appointmentService.getAppointments().subscribe({
      next: (data) => {
        this.appointments.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  getStatusSeverity(status: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (status.toLowerCase()) {
      case 'scheduled':
        return 'info';
      case 'completed':
        return 'success';
      case 'cancelled':
        return 'danger';
      case 'noshow':
        return 'warn';
      default:
        return 'secondary';
    }
  }
}
