import { Component, inject, OnInit, OnDestroy, signal, computed, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';

import { AppointmentService } from './data-access/appointment.service';
import { Appointment } from './data-access/appointment.model';
import { StaffService } from '../staff/data-access/staff.service';
import { Staff } from '../staff/data-access/staff.model';
import { PatientService } from '../patients/data-access/patient.service';
import { Patient } from '../patients/data-access/patient.model';
import { AppointmentSignalRService } from '../../core/services/appointment-signalr.service';
import Swal from 'sweetalert2';

interface DateGroup {
  dateKey: string;
  date: Date;
  dayName: string;
  dateDisplay: string;
  isToday: boolean;
  items: Appointment[];
}

@Component({
  selector: 'app-appointment-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './appointment-list.html',
})
export class AppointmentList implements OnInit, OnDestroy {
  private appointmentService = inject(AppointmentService);
  private staffService = inject(StaffService);
  private patientService = inject(PatientService);
  public signalRService = inject(AppointmentSignalRService);
  private fb = inject(FormBuilder);

  private signalRSub?: Subscription;

  // Global Math object for template expressions
  Math = Math;

  // View state
  viewMode = signal<'table' | 'schedule'>('table');
  appointments = signal<Appointment[]>([]);
  staffList = signal<Staff[]>([]);
  patients = signal<Patient[]>([]);
  isLoading = signal<boolean>(true);

  // Filters & Search
  searchQuery = signal<string>('');
  selectedStaffId = signal<string>('');
  selectedStatus = signal<string>('');

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Booking Modal State
  showBookingModal = signal<boolean>(false);
  bookingSubmitting = signal<boolean>(false);
  bookingFormSubmitted = signal<boolean>(false);
  bookingErrorMessage = signal<string | null>(null);
  patientSearch = signal<string>('');

  // Booking Form
  bookingForm = this.fb.group({
    patientId: ['', [Validators.required]],
    staffId: ['', [Validators.required]],
    startDateTime: ['', [Validators.required]],
    endDateTime: ['', [Validators.required]],
    reason: ['', [Validators.required]],
    notes: [''],
  });

  // Filtered Appointments List
  filteredAppointments = computed(() => {
    let list = this.appointments();

    const query = this.searchQuery().toLowerCase().trim();
    if (query) {
      list = list.filter(
        (a) =>
          a.patientName?.toLowerCase().includes(query) ||
          a.doctorName?.toLowerCase().includes(query) ||
          a.reason?.toLowerCase().includes(query)
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

  // Paginated Appointments for Table View
  paginatedAppointments = computed(() => {
    const list = this.filteredAppointments();
    const page = this.currentPage();
    const size = this.pageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  totalPages = computed(() => {
    const count = this.filteredAppointments().length;
    return Math.ceil(count / this.pageSize()) || 1;
  });

  // Patient Options for Modal Select
  patientOptions = computed(() => {
    const query = this.patientSearch().toLowerCase().trim();
    const list = this.patients();
    if (!query) return list;
    return list.filter(
      (p) =>
        p.firstName?.toLowerCase().includes(query) ||
        p.lastName?.toLowerCase().includes(query) ||
        p.medicalRecordNumber?.toLowerCase().includes(query)
    );
  });

  // Grouped Schedule by Date for Schedule Grid View
  groupedSchedule = computed<DateGroup[]>(() => {
    const list = this.filteredAppointments();
    if (!list || list.length === 0) return [];

    const groupsMap = new Map<string, { dateObj: Date; items: Appointment[] }>();
    const now = new Date();
    const todayStr = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;

    for (const apt of list) {
      const aptDate = new Date(apt.startDateTimeUtc);
      if (isNaN(aptDate.getTime())) continue;

      const dateKey = `${aptDate.getFullYear()}-${String(aptDate.getMonth() + 1).padStart(2, '0')}-${String(aptDate.getDate()).padStart(2, '0')}`;

      if (!groupsMap.has(dateKey)) {
        groupsMap.set(dateKey, { dateObj: aptDate, items: [] });
      }
      groupsMap.get(dateKey)!.items.push(apt);
    }

    // Convert map to sorted array by date ascending
    const sortedEntries = Array.from(groupsMap.entries()).sort((a, b) => a[0].localeCompare(b[0]));

    const sortedGroups: DateGroup[] = sortedEntries.map(([dateKey, groupData]) => {
      const dateObj = groupData.dateObj;
      const dayName = dateObj.toLocaleDateString('en-US', { weekday: 'short' }).toUpperCase();
      const dateDisplay = dateObj.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' }).toUpperCase();
      const isToday = dateKey === todayStr;

      // Sort items inside date group by start time ascending
      const sortedItems = [...groupData.items].sort(
        (a, b) => new Date(a.startDateTimeUtc).getTime() - new Date(b.startDateTimeUtc).getTime()
      );

      return {
        dateKey,
        date: dateObj,
        dayName,
        dateDisplay,
        isToday,
        items: sortedItems,
      };
    });

    return sortedGroups;
  });

  // Patient Map for fast MRN lookup
  private patientMap = computed(() => {
    const map = new Map<string, Patient>();
    for (const p of this.patients()) {
      map.set(p.id, p);
    }
    return map;
  });

  ngOnInit(): void {
    this.loadAppointments();
    this.loadStaff();
    this.loadPatients();

    // Real-time updates via SignalR
    this.signalRService.startConnection();
    this.signalRSub = this.signalRService.appointmentUpdated$.subscribe(() => {
      this.loadAppointments();
    });
  }

  ngOnDestroy(): void {
    this.signalRSub?.unsubscribe();
    this.signalRService.stopConnection();
  }

  @HostListener('window:keydown.escape')
  onEscapeKey(): void {
    if (this.showBookingModal()) {
      this.closeBookingModal();
    }
  }

  loadAppointments(): void {
    this.isLoading.set(true);
    this.appointmentService.getAppointments().subscribe({
      next: (data) => {
        this.appointments.set(data || []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  loadStaff(): void {
    this.staffService.getStaff().subscribe({
      next: (data) => this.staffList.set(data || []),
    });
  }

  loadPatients(): void {
    this.patientService.getPatients().subscribe({
      next: (data) => this.patients.set(data || []),
    });
  }

  // Modal Handlers
  openBookingModal(): void {
    this.bookingFormSubmitted.set(false);
    this.bookingErrorMessage.set(null);
    this.patientSearch.set('');
    this.bookingForm.reset();

    // Set default start/end date time (Start: today + 1hr, End: today + 1.5hr)
    const now = new Date();
    now.setMinutes(0, 0, 0);
    now.setHours(now.getHours() + 1);

    const end = new Date(now.getTime() + 30 * 60000);

    const formatForInput = (d: Date) => {
      const year = d.getFullYear();
      const month = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      const hours = String(d.getHours()).padStart(2, '0');
      const minutes = String(d.getMinutes()).padStart(2, '0');
      return `${year}-${month}-${day}T${hours}:${minutes}`;
    };

    this.bookingForm.patchValue({
      startDateTime: formatForInput(now),
      endDateTime: formatForInput(end),
    });

    this.showBookingModal.set(true);
    document.body.style.overflow = 'hidden';
  }

  closeBookingModal(): void {
    this.showBookingModal.set(false);
    this.bookingFormSubmitted.set(false);
    this.bookingErrorMessage.set(null);
    this.patientSearch.set('');
    this.bookingForm.reset();
    document.body.style.overflow = '';
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.closeBookingModal();
    }
  }

  onBookingSubmit(): void {
    this.bookingFormSubmitted.set(true);

    if (this.bookingForm.invalid) {
      return;
    }

    const val = this.bookingForm.value;
    const startObj = new Date(val.startDateTime!);
    const endObj = new Date(val.endDateTime!);

    if (isNaN(startObj.getTime()) || isNaN(endObj.getTime())) {
      this.bookingErrorMessage.set('Please provide valid start and end dates.');
      return;
    }

    if (endObj <= startObj) {
      this.bookingErrorMessage.set('End Date & Time must be after Start Date & Time.');
      return;
    }

    this.bookingSubmitting.set(true);
    this.bookingErrorMessage.set(null);

    const payload = {
      patientId: val.patientId!,
      staffId: val.staffId!,
      startDateTimeUtc: startObj.toISOString(),
      endDateTimeUtc: endObj.toISOString(),
      reason: val.reason!.trim(),
      notes: val.notes?.trim() || undefined,
    };

    this.appointmentService.bookAppointment(payload).subscribe({
      next: () => {
        this.bookingSubmitting.set(false);
        this.closeBookingModal();
        this.loadAppointments();

        Swal.fire({
          title: 'Appointment Scheduled',
          text: 'The patient consultation has been booked successfully.',
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2200,
          timerProgressBar: true,
          toast: false,
        });
      },
      error: (err) => {
        this.bookingSubmitting.set(false);
        const detail = err.error?.detail || err.error?.message || 'Failed to schedule appointment. Please try again.';
        this.bookingErrorMessage.set(detail);
      },
    });
  }

  // Update Status Action
  onUpdateStatus(id: string, status: number): void {
    this.appointmentService.updateStatus(id, status).subscribe({
      next: () => {
        Swal.fire({
          title: 'Status Updated',
          text: 'Appointment status updated successfully.',
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2000,
          timerProgressBar: true,
        });
        this.loadAppointments();
      },
    });
  }

  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedStaffId.set('');
    this.selectedStatus.set('');
    this.currentPage.set(1);
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  // Helpers
  getPatientMRN(apt: Appointment): string | null {
    if (!apt.patientId) return null;
    const p = this.patientMap().get(apt.patientId);
    return p?.medicalRecordNumber || null;
  }

  getPatientInitials(name: string): string {
    if (!name) return 'PT';
    const parts = name.trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  getInitials(name: string): string {
    return this.getPatientInitials(name);
  }

  formatDate(isoString: string): string {
    if (!isoString) return '';
    const d = new Date(isoString);
    if (isNaN(d.getTime())) return isoString;
    return d.toLocaleDateString('en-US', {
      month: 'short',
      day: '2-digit',
      year: 'numeric',
    });
  }

  formatTimeRange(startIso: string, endIso: string): string {
    if (!startIso) return '';
    const start = new Date(startIso);
    if (isNaN(start.getTime())) return '';

    const startFormatted = start.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });

    if (!endIso) return startFormatted;
    const end = new Date(endIso);
    if (isNaN(end.getTime())) return startFormatted;

    const endFormatted = end.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });

    return `${startFormatted} – ${endFormatted}`;
  }

  getStatusBadgeClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'scheduled':
        return 'bg-blue-50 text-blue-700 border-blue-200';
      case 'confirmed':
        return 'bg-teal-50 text-teal-700 border-teal-200';
      case 'completed':
        return 'bg-emerald-50 text-emerald-700 border-emerald-200';
      case 'cancelled':
        return 'bg-rose-50 text-rose-700 border-rose-200';
      case 'noshow':
      case 'no show':
      case 'no-show':
        return 'bg-amber-50 text-amber-700 border-amber-200';
      case 'in progress':
      case 'inprogress':
        return 'bg-teal-50 text-teal-700 border-teal-200';
      default:
        return 'bg-slate-50 text-slate-700 border-slate-200';
    }
  }

  getStatusDotClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'completed':
        return 'bg-emerald-500';
      case 'confirmed':
        return 'bg-teal-500';
      case 'scheduled':
        return 'bg-blue-500';
      case 'cancelled':
        return 'bg-rose-500';
      case 'noshow':
      case 'no show':
      case 'no-show':
        return 'bg-amber-500';
      default:
        return 'bg-slate-400';
    }
  }

  getStatusAccentBorder(status: string): string {
    switch (status?.toLowerCase()) {
      case 'completed':
        return 'border-l-4 border-l-emerald-500';
      case 'confirmed':
        return 'border-l-4 border-l-teal-500';
      case 'scheduled':
        return 'border-l-4 border-l-blue-500';
      case 'cancelled':
        return 'border-l-4 border-l-rose-500';
      case 'noshow':
      case 'no show':
      case 'no-show':
        return 'border-l-4 border-l-amber-500';
      default:
        return 'border-l-4 border-l-slate-400';
    }
  }

  getCardAccentClass(status: string): string {
    return this.getStatusAccentBorder(status);
  }
}
