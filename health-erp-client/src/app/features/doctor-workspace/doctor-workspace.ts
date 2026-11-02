import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DrawerModule } from 'primeng/drawer';
import { Subscription } from 'rxjs';
import { DoctorClinicalNoteForm } from './clinical-note-form';
import { DoctorWorkspaceService } from './data-access/doctor-workspace.service';
import { DoctorPatientSnapshot, DoctorSchedule, DoctorScheduleAppointment } from './data-access/doctor-workspace.model';

@Component({
  selector: 'app-doctor-workspace',
  standalone: true,
  imports: [DrawerModule, DoctorClinicalNoteForm],
  templateUrl: './doctor-workspace.html',
  styleUrl: './doctor-workspace.scss',
})
export class DoctorWorkspace implements OnInit, OnDestroy {
  private readonly api = inject(DoctorWorkspaceService);
  private scheduleRequest?: Subscription;
  private snapshotRequest?: Subscription;
  private requestedDate?: string;
  readonly schedule = signal<DoctorSchedule | null>(null);
  readonly selectedDate = signal<string | null>(null);
  readonly facilityToday = signal<string | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly snapshotOpen = signal(false);
  readonly snapshot = signal<DoctorPatientSnapshot | null>(null);
  readonly snapshotLoading = signal(false);
  readonly snapshotError = signal<string | null>(null);
  readonly noteFormOpen = signal(false);
  readonly noteSaving = signal(false);
  readonly noteSuccess = signal<string | null>(null);
  private selectedPatientId: string | null = null;
  readonly greeting = new Date().getHours() < 12 ? 'Good morning' : new Date().getHours() < 18 ? 'Good afternoon' : 'Good evening';

  ngOnInit() { this.loadSchedule(); }
  ngOnDestroy() { this.scheduleRequest?.unsubscribe(); this.snapshotRequest?.unsubscribe(); }

  loadSchedule(date?: string): void {
    if (this.noteSaving()) return;
    this.scheduleRequest?.unsubscribe();
    this.closeSnapshot();
    this.requestedDate = date;
    this.selectedDate.set(date ?? null);
    this.schedule.set(null);
    this.error.set(null);
    this.loading.set(true);
    this.scheduleRequest = this.api.getSchedule(date).subscribe({
      next: (data) => {
        this.schedule.set(data);
        this.selectedDate.set(data.date);
        if (date === undefined) this.facilityToday.set(data.date);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.error.set(error.status === 401 ? 'Your session has expired. Please sign in again.'
          : error.status === 403 ? 'Doctor Workspace access is not available for this account. Please contact your administrator.'
          : 'Your schedule could not be loaded. Please try again.');
      },
    });
  }

  retrySchedule(): void { this.loadSchedule(this.requestedDate); }

  moveDay(offset: -1 | 1): void {
    const selected = this.selectedDate();
    if (!selected) return;
    // Calendar arithmetic only: never use browser-local midnight for a clinical date.
    const day = new Date(`${selected}T00:00:00Z`);
    day.setUTCDate(day.getUTCDate() + offset);
    const date = day.toISOString().slice(0, 10);
    if (/^\d{4}-\d{2}-\d{2}$/.test(date) && date > '0000-12-31' && date < '9999-12-31') this.loadSchedule(date);
  }

  openSnapshot(appointment: DoctorScheduleAppointment): void {
    if (this.noteSaving()) return;
    this.noteFormOpen.set(false); this.noteSuccess.set(null);
    this.selectedPatientId = appointment.patientId;
    this.loadSnapshot();
  }

  retrySnapshot(): void { if (this.selectedPatientId && !this.noteSaving()) this.loadSnapshot(); }

  onNoteSaved(): void {
    this.noteFormOpen.set(false);
    this.noteSuccess.set('Clinical note saved. Refreshing recorded information.');
    this.loadSnapshot();
  }

  private loadSnapshot(): void {
    this.snapshotRequest?.unsubscribe();
    this.snapshot.set(null);
    this.snapshotError.set(null);
    this.snapshotLoading.set(true);
    this.snapshotOpen.set(true);
    this.snapshotRequest = this.api.getSnapshot(this.selectedPatientId!).subscribe({
      next: (data) => { this.snapshot.set(data); this.snapshotLoading.set(false); },
      error: (error) => {
        this.snapshotLoading.set(false);
        this.snapshotError.set(error.status === 404 ? 'Patient snapshot is not available.'
          : error.status === 401 ? 'Your session has expired. Please sign in again.'
          : error.status === 403 ? 'Patient snapshot access is not available.'
          : 'Patient snapshot could not be loaded. Please try again.');
      },
    });
  }

  closeSnapshot(): void {
    if (this.noteSaving()) return;
    this.noteFormOpen.set(false); this.noteSuccess.set(null);
    this.snapshotRequest?.unsubscribe();
    this.snapshotOpen.set(false);
    this.snapshot.set(null);
    this.snapshotError.set(null);
    this.snapshotLoading.set(false);
    this.selectedPatientId = null;
  }

  display(value: string | null | undefined): string { return value?.trim() || 'Unavailable'; }
  statusLabel(status: string): string { return status === 'NoShow' ? 'No Show' : status === 'InProgress' ? 'In progress' : status; }
  statusClass(status: string): string {
    return ({ Scheduled: 'scheduled', Confirmed: 'confirmed', Completed: 'completed', Cancelled: 'cancelled', NoShow: 'no-show' } as Record<string, string>)[status] ?? '';
  }
  calendarDate(date: string): string {
    return new Intl.DateTimeFormat('en', { timeZone: 'UTC', month: 'long', day: 'numeric', year: 'numeric' }).format(new Date(`${date}T00:00:00Z`));
  }
  time(utc: string): string {
    return new Intl.DateTimeFormat('en', { timeZone: this.schedule()!.timeZoneId, hour: 'numeric', minute: '2-digit' }).format(new Date(utc));
  }
  recorded(utc: string): string {
    return new Intl.DateTimeFormat('en', { timeZone: this.schedule()!.timeZoneId, month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }).format(new Date(utc));
  }
}
