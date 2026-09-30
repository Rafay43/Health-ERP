import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { BadgeModule } from 'primeng/badge';
import { TooltipModule } from 'primeng/tooltip';

import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { AppNotification } from '../../core/models/notification.model';
import { PatientService } from '../../features/patients/data-access/patient.service';
import { Patient } from '../../features/patients/data-access/patient.model';
import { StaffService } from '../../features/staff/data-access/staff.service';
import { Staff } from '../../features/staff/data-access/staff.model';
import { EmergencyService } from '../../features/emergency/data-access/emergency.service';
import { TriageRecord } from '../../features/emergency/data-access/emergency.model';
import { AppointmentService } from '../../features/appointments/data-access/appointment.service';
import { Appointment } from '../../features/appointments/data-access/appointment.model';

export interface SearchResult {
  id: string;
  type: 'Patient' | 'Staff' | 'Appointment' | 'Emergency';
  title: string;
  subtitle: string;
  route: string;
  icon: string;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    ButtonModule,
    AvatarModule,
    BadgeModule,
    TooltipModule
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell implements OnInit {
  private authService = inject(AuthService);
  public notificationService = inject(NotificationService);
  private patientService = inject(PatientService);
  private staffService = inject(StaffService);
  private emergencyService = inject(EmergencyService);
  private appointmentService = inject(AppointmentService);
  private router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  showNotificationDrawer = signal(false);

  // Global Quick-Search Signals
  searchQuery = signal('');
  showSearchDropdown = signal(false);

  private patients = signal<Patient[]>([]);
  private staffMembers = signal<Staff[]>([]);
  private emergencyRecords = signal<TriageRecord[]>([]);
  private appointments = signal<Appointment[]>([]);
  private dataLoaded = false;

  navItems = [
    { label: 'Dashboard', icon: 'pi pi-home', route: '/dashboard' },
    { label: 'Patients', icon: 'pi pi-users', route: '/patients' },
    { label: 'Appointments', icon: 'pi pi-calendar', route: '/appointments' },
    { label: 'Billing', icon: 'pi pi-wallet', route: '/billing' },
    { label: 'Pharmacy', icon: 'pi pi-heart', route: '/pharmacy' },
    { label: 'Laboratory', icon: 'pi pi-list', route: '/laboratory' },
    { label: 'Inpatient Wards', icon: 'pi pi-building', route: '/inpatient' },
    { label: 'Emergency (ER)', icon: 'pi pi-bolt', route: '/emergency' },
    { label: 'Staff', icon: 'pi pi-id-card', route: '/staff' },
    { label: 'Reports', icon: 'pi pi-chart-bar', route: '/reports' },
    { label: 'Admin', icon: 'pi pi-cog', route: '/admin' },
  ];

  sidebarCollapsed = false;

  ngOnInit() {
    this.notificationService.init();
  }

  loadSearchData(): void {
    if (this.dataLoaded) return;
    this.dataLoaded = true;

    this.patientService.getPatients().subscribe({
      next: (data) => this.patients.set(data),
      error: () => {}
    });
    this.staffService.getStaff().subscribe({
      next: (data) => this.staffMembers.set(data),
      error: () => {}
    });
    this.emergencyService.getEmergencyQueue(false).subscribe({
      next: (data) => this.emergencyRecords.set(data),
      error: () => {}
    });
    this.appointmentService.getAppointments().subscribe({
      next: (data) => this.appointments.set(data),
      error: () => {}
    });
  }

  onSearchFocus(): void {
    this.loadSearchData();
    this.showSearchDropdown.set(true);
  }

  onSearchInput(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.searchQuery.set(val);
    this.showSearchDropdown.set(true);
    this.loadSearchData();
  }

  clearSearch(): void {
    this.searchQuery.set('');
    this.showSearchDropdown.set(false);
  }

  searchResults = computed<SearchResult[]>(() => {
    const q = this.searchQuery().trim().toLowerCase();
    if (!q) return [];

    const results: SearchResult[] = [];

    // 1. Search Patients
    for (const p of this.patients()) {
      const name = `${p.firstName} ${p.lastName}`.toLowerCase();
      const mrn = (p.medicalRecordNumber || '').toLowerCase();
      if (name.includes(q) || mrn.includes(q)) {
        results.push({
          id: p.id,
          type: 'Patient',
          title: `${p.firstName} ${p.lastName}`,
          subtitle: `MRN: ${p.medicalRecordNumber} • DOB: ${p.dateOfBirth}`,
          route: `/patients/${p.id}`,
          icon: 'pi pi-user'
        });
      }
    }

    // 2. Search Staff
    for (const s of this.staffMembers()) {
      const name = `${s.firstName} ${s.lastName}`.toLowerCase();
      const spec = (s.specialty || '').toLowerCase();
      const dept = (s.department || '').toLowerCase();
      if (name.includes(q) || spec.includes(q) || dept.includes(q)) {
        results.push({
          id: s.id,
          type: 'Staff',
          title: `Dr. ${s.firstName} ${s.lastName}`,
          subtitle: `${s.specialty} • ${s.department}`,
          route: '/staff',
          icon: 'pi pi-id-card'
        });
      }
    }

    // 3. Search Emergency Triage
    for (const e of this.emergencyRecords()) {
      const pName = (e.patientName || '').toLowerCase();
      const complaint = (e.chiefComplaint || '').toLowerCase();
      const nurse = (e.triageNurseName || '').toLowerCase();
      if (pName.includes(q) || complaint.includes(q) || nurse.includes(q)) {
        results.push({
          id: e.id,
          type: 'Emergency',
          title: `${e.patientName}`,
          subtitle: `Chief Complaint: ${e.chiefComplaint} • Nurse: ${e.triageNurseName}`,
          route: '/emergency',
          icon: 'pi pi-bolt'
        });
      }
    }

    // 4. Search Appointments
    for (const a of this.appointments()) {
      const pName = (a.patientName || '').toLowerCase();
      const reason = (a.reason || '').toLowerCase();
      const doc = (a.doctorName || '').toLowerCase();
      if (pName.includes(q) || reason.includes(q) || doc.includes(q)) {
        results.push({
          id: a.id,
          type: 'Appointment',
          title: `Appt: ${a.patientName}`,
          subtitle: `Doctor: ${a.doctorName || 'Assigned Doctor'} • Reason: ${a.reason}`,
          route: '/appointments',
          icon: 'pi pi-calendar'
        });
      }
    }

    return results.slice(0, 10);
  });

  selectSearchResult(res: SearchResult): void {
    this.router.navigateByUrl(res.route);
    this.clearSearch();
  }

  toggleSidebar() {
    this.sidebarCollapsed = !this.sidebarCollapsed;
  }

  toggleNotificationDrawer() {
    this.showNotificationDrawer.update(v => !v);
  }

  markAsRead(n: AppNotification, event: Event) {
    event.stopPropagation();
    this.notificationService.markAsRead(n.id);
  }

  markAllAsRead() {
    this.notificationService.markAllAsRead();
  }

  onNotificationClick(n: AppNotification) {
    this.notificationService.markAsRead(n.id);
    if (n.linkUrl) {
      this.router.navigateByUrl(n.linkUrl);
      this.showNotificationDrawer.set(false);
    }
  }

  getNotificationSeverityClass(severity: string | number): string {
    const sev = String(severity).toLowerCase();
    if (sev.includes('critical') || sev === '3') return 'border-l-4 border-rose-500 bg-rose-50/50';
    if (sev.includes('warning') || sev === '2') return 'border-l-4 border-amber-500 bg-amber-50/50';
    return 'border-l-4 border-blue-500 bg-blue-50/50';
  }

  logout() {
    this.authService.logout();
  }

  get userInitials(): string {
    const u = this.currentUser();
    if (!u) return 'U';
    return `${u.firstName?.[0] || ''}${u.lastName?.[0] || ''}`.toUpperCase() || 'U';
  }
}