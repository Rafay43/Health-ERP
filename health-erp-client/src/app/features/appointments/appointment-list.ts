import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { InputTextModule } from 'primeng/inputtext';

import { AppointmentService } from './data-access/appointment.service';
import { Appointment } from './data-access/appointment.model';
import { StaffService } from '../staff/data-access/staff.service';
import { Staff } from '../staff/data-access/staff.model';
import { AppointmentSignalRService } from '../../core/services/appointment-signalr.service';

@Component({
  selector: 'app-appointment-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    TableModule,
    ButtonModule,
    TagModule,
    TooltipModule,
    InputTextModule,
  ],
  template: `
    <div class="flex flex-col gap-5">
      <!-- Header Bar -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Appointments & Scheduling</h1>
          <p class="text-sm text-[var(--color-text-secondary)]">Manage scheduled patient consultations and doctor agendas</p>
        </div>
        <div class="flex items-center gap-3">
          <!-- View Switcher -->
          <div class="inline-flex rounded-md border border-[var(--color-border)] p-1 bg-[var(--color-surface)]">
            <button
              type="button"
              (click)="viewMode.set('table')"
              [class.bg-[var(--color-primary)]]="viewMode() === 'table'"
              [class.text-white]="viewMode() === 'table'"
              [class.text-[var(--color-text-secondary)]]="viewMode() !== 'table'"
              class="px-3 py-1.5 text-xs font-semibold rounded flex items-center gap-1.5 transition-colors"
            >
              <i class="pi pi-list"></i> Table View
            </button>
            <button
              type="button"
              (click)="viewMode.set('schedule')"
              [class.bg-[var(--color-primary)]]="viewMode() === 'schedule'"
              [class.text-white]="viewMode() === 'schedule'"
              [class.text-[var(--color-text-secondary)]]="viewMode() !== 'schedule'"
              class="px-3 py-1.5 text-xs font-semibold rounded flex items-center gap-1.5 transition-colors"
            >
              <i class="pi pi-calendar font-bold"></i> Schedule Grid
            </button>
          </div>

          <p-button
            label="Book Appointment"
            icon="pi pi-calendar-plus"
            routerLink="/appointments/book"
          ></p-button>
        </div>
      </div>

      <!-- Filters & Controls Bar -->
      <div class="bg-[var(--color-surface)] p-4 rounded-lg border border-[var(--color-border)] flex flex-wrap items-center gap-4">
        <!-- Search Input -->
        <div class="flex-1 min-w-[200px]">
          <div class="relative">
            <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"></i>
            <input
              type="text"
              pInputText
              [(ngModel)]="searchQuery"
              placeholder="Search patient, doctor, or reason..."
              class="w-full pl-9 text-sm"
            />
          </div>
        </div>

        <!-- Doctor / Staff Filter -->
        <div class="flex items-center gap-2">
          <label class="text-xs font-semibold text-[var(--color-text-secondary)] uppercase tracking-wider">Doctor:</label>
          <select
            [(ngModel)]="selectedStaffId"
            class="bg-[var(--color-bg)] border border-[var(--color-border)] text-[var(--color-text-primary)] rounded px-3 py-1.5 text-sm focus:outline-none focus:border-[var(--color-primary)]"
          >
            <option value="">All Staff / Doctors</option>
            <option *ngFor="let staff of staffList()" [value]="staff.id">
              Dr. {{ staff.firstName }} {{ staff.lastName }} ({{ staff.specialty }})
            </option>
          </select>
        </div>

        <!-- Status Filter -->
        <div class="flex items-center gap-2">
          <label class="text-xs font-semibold text-[var(--color-text-secondary)] uppercase tracking-wider">Status:</label>
          <select
            [(ngModel)]="selectedStatus"
            class="bg-[var(--color-bg)] border border-[var(--color-border)] text-[var(--color-text-primary)] rounded px-3 py-1.5 text-sm focus:outline-none focus:border-[var(--color-primary)]"
          >
            <option value="">All Statuses</option>
            <option value="Scheduled">Scheduled</option>
            <option value="Confirmed">Confirmed</option>
            <option value="Completed">Completed</option>
            <option value="Cancelled">Cancelled</option>
            <option value="NoShow">No-Show</option>
          </select>
        </div>
      </div>

      <!-- View Container -->
      @if (viewMode() === 'table') {
        <!-- Table View -->
        <div class="bg-[var(--color-surface)] rounded-lg border border-[var(--color-border)] p-4">
          <p-table [value]="filteredAppointments()" [loading]="isLoading()" [paginator]="true" [rows]="10">
            <ng-template pTemplate="header">
              <tr>
                <th>Patient</th>
                <th>Doctor / Staff</th>
                <th>Start Time</th>
                <th>End Time</th>
                <th>Reason</th>
                <th>Status</th>
                <th class="text-center">Actions</th>
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
                <td>
                  <div class="flex items-center justify-center gap-1">
                    @if (apt.status === 'Scheduled') {
                      <p-button
                        icon="pi pi-check-circle"
                        severity="info"
                        [rounded]="true"
                        [text]="true"
                        pTooltip="Confirm Appointment"
                        (onClick)="onUpdateStatus(apt.id, 2)"
                      ></p-button>
                    }
                    @if (apt.status === 'Scheduled' || apt.status === 'Confirmed') {
                      <p-button
                        icon="pi pi-check"
                        severity="success"
                        [rounded]="true"
                        [text]="true"
                        pTooltip="Mark Completed"
                        (onClick)="onUpdateStatus(apt.id, 3)"
                      ></p-button>
                      <p-button
                        icon="pi pi-times"
                        severity="danger"
                        [rounded]="true"
                        [text]="true"
                        pTooltip="Cancel Appointment"
                        (onClick)="onUpdateStatus(apt.id, 4)"
                      ></p-button>
                      <p-button
                        icon="pi pi-user-minus"
                        severity="warn"
                        [rounded]="true"
                        [text]="true"
                        pTooltip="Mark No-Show"
                        (onClick)="onUpdateStatus(apt.id, 5)"
                      ></p-button>
                    }
                  </div>
                </td>
              </tr>
            </ng-template>

            <ng-template pTemplate="emptymessage">
              <tr>
                <td colspan="7" class="text-center py-8 text-[var(--color-text-secondary)]">
                  No appointments match the selected filter criteria. Click "Book Appointment" to schedule one.
                </td>
              </tr>
            </ng-template>
          </p-table>
        </div>
      } @else {
        <!-- Schedule Grid View -->
        <div class="bg-[var(--color-surface)] rounded-lg border border-[var(--color-border)] p-6">
          <div class="flex items-center justify-between mb-4 pb-2 border-b border-[var(--color-border)]">
            <h3 class="text-base font-semibold text-[var(--color-text-primary)] flex items-center gap-2">
              <i class="pi pi-clock text-[var(--color-primary)]"></i> Daily Consultation Schedule Grid
            </h3>
            <span class="text-xs text-[var(--color-text-secondary)]">Showing {{ filteredAppointments().length }} consultations</span>
          </div>

          @if (filteredAppointments().length === 0) {
            <div class="text-center py-12 text-[var(--color-text-secondary)] flex flex-col items-center gap-2">
              <i class="pi pi-calendar-times text-4xl text-slate-300"></i>
              <p class="font-medium">No consultations scheduled for the selected filters.</p>
            </div>
          } @else {
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              <div
                *ngFor="let apt of filteredAppointments()"
                class="rounded-lg border p-4 flex flex-col justify-between transition-all hover:shadow-md bg-[var(--color-bg)]"
                [ngClass]="getCardBorderClass(apt.status)"
              >
                <div>
                  <div class="flex items-center justify-between gap-2 mb-2">
                    <span class="text-xs font-semibold px-2 py-0.5 rounded flex items-center gap-1" [ngClass]="getBadgeClass(apt.status)">
                      <i class="pi pi-circle-fill text-[8px]"></i> {{ apt.status }}
                    </span>
                    <span class="text-xs text-[var(--color-text-secondary)] font-mono">
                      {{ apt.startDateTimeUtc | date:'shortTime' }} - {{ apt.endDateTimeUtc | date:'shortTime' }}
                    </span>
                  </div>

                  <h4 class="font-bold text-base text-[var(--color-text-primary)] mb-1">
                    {{ apt.patientName }}
                  </h4>

                  <p class="text-xs font-medium text-[var(--color-primary)] mb-2 flex items-center gap-1">
                    <i class="pi pi-user-md"></i> {{ apt.doctorName }}
                  </p>

                  <p class="text-xs text-[var(--color-text-secondary)] line-clamp-2 bg-[var(--color-surface)] p-2 rounded border border-[var(--color-border)]">
                    <span class="font-semibold text-slate-700">Reason:</span> {{ apt.reason }}
                  </p>
                </div>

                <div class="flex items-center justify-between pt-3 mt-3 border-t border-[var(--color-border)]">
                  <span class="text-[10px] text-[var(--color-text-secondary)]">
                    {{ apt.startDateTimeUtc | date:'MMM d, yyyy' }}
                  </span>
                  
                  <div class="flex items-center gap-1">
                    @if (apt.status === 'Scheduled') {
                      <button
                        (click)="onUpdateStatus(apt.id, 2)"
                        title="Confirm Appointment"
                        class="p-1 rounded text-blue-600 hover:bg-blue-50 text-xs font-medium flex items-center gap-0.5"
                      >
                        <i class="pi pi-check-circle"></i> Confirm
                      </button>
                    }
                    @if (apt.status === 'Scheduled' || apt.status === 'Confirmed') {
                      <button
                        (click)="onUpdateStatus(apt.id, 3)"
                        title="Complete Consultation"
                        class="p-1 rounded text-emerald-600 hover:bg-emerald-50 text-xs font-medium flex items-center gap-0.5"
                      >
                        <i class="pi pi-check"></i> Complete
                      </button>
                      <button
                        (click)="onUpdateStatus(apt.id, 4)"
                        title="Cancel"
                        class="p-1 rounded text-rose-600 hover:bg-rose-50 text-xs font-medium flex items-center gap-0.5"
                      >
                        <i class="pi pi-times"></i> Cancel
                      </button>
                    }
                  </div>
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AppointmentList implements OnInit, OnDestroy {
  private appointmentService = inject(AppointmentService);
  private staffService = inject(StaffService);
  public signalRService = inject(AppointmentSignalRService);

  private signalRSub?: Subscription;

  viewMode = signal<'table' | 'schedule'>('table');
  appointments = signal<Appointment[]>([]);
  staffList = signal<Staff[]>([]);
  isLoading = signal(true);

  // Filters
  searchQuery = signal<string>('');
  selectedStaffId = signal<string>('');
  selectedStatus = signal<string>('');

  // Computed Filtered Appointments
  filteredAppointments = computed(() => {
    let list = this.appointments();

    const query = this.searchQuery().toLowerCase().trim();
    if (query) {
      list = list.filter(
        (a) =>
          a.patientName.toLowerCase().includes(query) ||
          a.doctorName.toLowerCase().includes(query) ||
          a.reason.toLowerCase().includes(query)
      );
    }

    const staffId = this.selectedStaffId();
    if (staffId) {
      list = list.filter((a) => a.staffId === staffId);
    }

    const status = this.selectedStatus();
    if (status) {
      list = list.filter((a) => a.status.toLowerCase() === status.toLowerCase());
    }

    return list;
  });

  ngOnInit(): void {
    this.loadAppointments();
    this.loadStaff();

    // SignalR Real-time Connection
    this.signalRService.startConnection();
    this.signalRSub = this.signalRService.appointmentUpdated$.subscribe(() => {
      this.loadAppointments();
    });
  }

  ngOnDestroy(): void {
    this.signalRSub?.unsubscribe();
    this.signalRService.stopConnection();
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

  loadStaff(): void {
    this.staffService.getStaff().subscribe({
      next: (data) => this.staffList.set(data),
    });
  }

  onUpdateStatus(id: string, status: number): void {
    this.appointmentService.updateStatus(id, status).subscribe({
      next: () => this.loadAppointments(),
    });
  }

  getStatusSeverity(status: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (status.toLowerCase()) {
      case 'scheduled':
        return 'secondary';
      case 'confirmed':
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

  getCardBorderClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'confirmed':
        return 'border-blue-300';
      case 'completed':
        return 'border-emerald-300';
      case 'cancelled':
        return 'border-rose-200 opacity-60';
      case 'noshow':
        return 'border-amber-300';
      default:
        return 'border-slate-300';
    }
  }

  getBadgeClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'confirmed':
        return 'bg-blue-100 text-blue-700';
      case 'completed':
        return 'bg-emerald-100 text-emerald-700';
      case 'cancelled':
        return 'bg-rose-100 text-rose-700';
      case 'noshow':
        return 'bg-amber-100 text-amber-700';
      default:
        return 'bg-slate-100 text-slate-700';
    }
  }
}
