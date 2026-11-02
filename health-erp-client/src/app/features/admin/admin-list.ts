import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';

import { AdminService } from './data-access/admin.service';
import { UserAccount, AuditLogEntry, SystemHealth } from './data-access/admin.model';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-admin-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    TagModule,
    TooltipModule,
    InputTextModule,
    DialogModule,
  ],
  template: `
    <div class="flex flex-col gap-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">System Admin & Security Control Panel</h1>
          <p class="text-sm text-[var(--color-text-secondary)]">Manage user account permissions, role-based access control, system health, and entity audit logs</p>
        </div>
        <div *ngIf="health() as h" class="flex items-center gap-3 bg-[var(--color-surface)] px-4 py-2 rounded-xl border border-[var(--color-border)] shadow-xs">
          <div class="flex items-center gap-2">
            <span class="w-3 h-3 rounded-full animate-pulse" [ngClass]="h.databaseCanConnect ? 'bg-emerald-500' : 'bg-rose-500'"></span>
            <span class="text-xs font-bold text-slate-800">Database: {{ h.status }}</span>
          </div>
          <span class="text-xs text-slate-500">| {{ h.databaseProvider }}</span>
        </div>
      </div>

      <!-- Main Navigation Tabs -->
      <div class="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] shadow-xs overflow-hidden">
        <div class="flex border-b border-[var(--color-border)] bg-[var(--color-surface-subtle)]">
          <button
            class="px-6 py-3 font-semibold text-sm transition-colors border-b-2 flex items-center gap-2"
            [class.border-indigo-600]="activeTab() === 'users'"
            [class.text-indigo-600]="activeTab() === 'users'"
            [class.border-transparent]="activeTab() !== 'users'"
            [class.text-[var(--color-text-secondary)]]="activeTab() !== 'users'"
            (click)="activeTab.set('users')"
          >
            <i class="pi pi-users"></i>
            User Accounts & Roles ({{ users().length }})
          </button>
          <button
            class="px-6 py-3 font-semibold text-sm transition-colors border-b-2 flex items-center gap-2"
            [class.border-indigo-600]="activeTab() === 'audit'"
            [class.text-indigo-600]="activeTab() === 'audit'"
            [class.border-transparent]="activeTab() !== 'audit'"
            [class.text-[var(--color-text-secondary)]]="activeTab() !== 'audit'"
            (click)="activeTab.set('audit')"
          >
            <i class="pi pi-shield"></i>
            System Audit Trail ({{ auditLogs().length }})
          </button>
          <button
            class="px-6 py-3 font-semibold text-sm transition-colors border-b-2 flex items-center gap-2"
            [class.border-indigo-600]="activeTab() === 'system'"
            [class.text-indigo-600]="activeTab() === 'system'"
            [class.border-transparent]="activeTab() !== 'system'"
            [class.text-[var(--color-text-secondary)]]="activeTab() !== 'system'"
            (click)="activeTab.set('system')"
          >
            <i class="pi pi-cog"></i>
            Environment Diagnostics
          </button>
        </div>

        <!-- Users & Roles Tab Content -->
        <div *ngIf="activeTab() === 'users'" class="p-4 flex flex-col gap-4">
          <div class="flex items-center justify-between gap-4">
            <h3 class="text-base font-bold text-[var(--color-text-primary)]">System User Directory & Access Rights</h3>
            <span class="text-xs text-slate-500">Active Roles: Admin, Doctor, Nurse, Staff</span>
          </div>

          <p-table
            [value]="users()"
            [loading]="loading()"
            [paginator]="true"
            [rows]="10"
            [tableStyle]="{ 'min-width': '50rem' }"
            styleClass="p-datatable-sm p-datatable-gridlines"
          >
            <ng-template pTemplate="header">
              <tr>
                <th>User Full Name</th>
                <th>Email Address / Username</th>
                <th>Assigned Roles</th>
                <th>Account Status</th>
                <th>Registered Date</th>
                <th class="text-center">Action</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-u>
              <tr>
                <td class="font-bold text-[var(--color-text-primary)]">
                  {{ u.firstName }} {{ u.lastName }}
                </td>
                <td class="text-slate-700">{{ u.email }}</td>
                <td>
                  <div class="flex flex-wrap gap-1">
                    <span *ngFor="let role of u.roles" class="px-2 py-0.5 bg-indigo-100 text-indigo-700 font-bold rounded text-xs">
                      {{ role }}
                    </span>
                    <span *ngIf="u.roles.length === 0" class="text-xs text-slate-400 italic">No role assigned</span>
                  </div>
                </td>
                <td>
                  <p-tag
                    [severity]="u.isActive ? 'success' : 'danger'"
                    [value]="u.isActive ? 'Active' : 'Locked'"
                  ></p-tag>
                </td>
                <td>{{ u.createdAtUtc | date:'shortDate' }}</td>
                <td class="text-center">
                  <div class="flex items-center justify-center gap-2">
                    <p-button
                      icon="pi pi-user-edit"
                      label="Assign Role"
                      styleClass="p-button-outlined p-button-sm p-button-primary"
                      (click)="openAssignRoleDialog(u)"
                    ></p-button>
                    <p-button
                      [icon]="u.isActive ? 'pi pi-lock' : 'pi pi-lock-open'"
                      [label]="u.isActive ? 'Lock Account' : 'Unlock'"
                      [styleClass]="u.isActive ? 'p-button-text p-button-sm p-button-danger' : 'p-button-text p-button-sm p-button-success'"
                      (click)="toggleAccountStatus(u)"
                    ></p-button>
                  </div>
                </td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr>
                <td colspan="6" class="text-center py-8 text-slate-500">
                  No user accounts found.
                </td>
              </tr>
            </ng-template>
          </p-table>
        </div>

        <!-- Audit Trail Tab Content -->
        <div *ngIf="activeTab() === 'audit'" class="p-4 flex flex-col gap-4">
          <div class="flex items-center justify-between gap-4">
            <h3 class="text-base font-bold text-[var(--color-text-primary)]">System-Wide Entity Audit Trail Log</h3>
            <div class="flex items-center gap-2">
              <p-button
                label="All Entities"
                [styleClass]="auditFilter() === null ? 'p-button-primary p-button-sm' : 'p-button-outlined p-button-secondary p-button-sm'"
                (click)="setAuditFilter(null)"
              ></p-button>
              <p-button
                label="Patients"
                [styleClass]="auditFilter() === 'Patient' ? 'p-button-info p-button-sm' : 'p-button-outlined p-button-secondary p-button-sm'"
                (click)="setAuditFilter('Patient')"
              ></p-button>
              <p-button
                label="Prescriptions"
                [styleClass]="auditFilter() === 'Prescription' ? 'p-button-warning p-button-sm' : 'p-button-outlined p-button-secondary p-button-sm'"
                (click)="setAuditFilter('Prescription')"
              ></p-button>
              <p-button
                label="Lab Orders"
                [styleClass]="auditFilter() === 'LabOrder' ? 'p-button-help p-button-sm' : 'p-button-outlined p-button-secondary p-button-sm'"
                (click)="setAuditFilter('LabOrder')"
              ></p-button>
              <p-button
                label="Invoices"
                [styleClass]="auditFilter() === 'Invoice' ? 'p-button-success p-button-sm' : 'p-button-outlined p-button-secondary p-button-sm'"
                (click)="setAuditFilter('Invoice')"
              ></p-button>
            </div>
          </div>

          <p-table
            [value]="filteredAuditLogs()"
            [loading]="loading()"
            [paginator]="true"
            [rows]="10"
            [tableStyle]="{ 'min-width': '55rem' }"
            styleClass="p-datatable-sm p-datatable-gridlines"
          >
            <ng-template pTemplate="header">
              <tr>
                <th>Timestamp</th>
                <th>Target Entity</th>
                <th>Action</th>
                <th>Performed By</th>
                <th>Modification Summary</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-log>
              <tr>
                <td class="text-xs text-slate-500">{{ log.timestampUtc | date:'medium' }}</td>
                <td>
                  <span class="px-2 py-0.5 bg-slate-100 text-slate-800 font-bold rounded text-xs">
                    {{ log.entityName }}
                  </span>
                </td>
                <td>
                  <span
                    class="font-semibold text-xs px-2 py-0.5 rounded"
                    [ngClass]="{
                      'bg-emerald-100 text-emerald-700': log.action === 'Created' || log.action === 'Result Recorded',
                      'bg-blue-100 text-blue-700': log.action === 'Modified' || log.action === 'Payment Recorded',
                      'bg-amber-100 text-amber-700': log.action === 'Dispensed'
                    }"
                  >
                    {{ log.action }}
                  </span>
                </td>
                <td class="font-semibold text-slate-900">{{ log.performedBy }}</td>
                <td class="text-xs text-slate-700">{{ log.details }}</td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr>
                <td colspan="5" class="text-center py-8 text-slate-500">
                  No audit log records match filter.
                </td>
              </tr>
            </ng-template>
          </p-table>
        </div>

        <!-- System Diagnostics Tab Content -->
        <div *ngIf="activeTab() === 'system'" class="p-6 flex flex-col gap-6">
          <div *ngIf="health() as h" class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div class="p-5 bg-slate-50 border border-slate-200 rounded-xl">
              <p class="text-xs font-bold text-slate-500 uppercase">Database Engine</p>
              <h4 class="text-lg font-bold text-slate-900 mt-1">{{ h.databaseProvider }}</h4>
              <p class="text-xs text-emerald-600 font-semibold mt-2">Active Connection OK</p>
            </div>

            <div class="p-5 bg-slate-50 border border-slate-200 rounded-xl">
              <p class="text-xs font-bold text-slate-500 uppercase">Total User Accounts</p>
              <h4 class="text-lg font-bold text-slate-900 mt-1">{{ h.totalUsers }} Registered Users</h4>
              <p class="text-xs text-indigo-600 font-semibold mt-2">{{ h.activeRolesCount }} Active System Roles</p>
            </div>

            <div class="p-5 bg-slate-50 border border-slate-200 rounded-xl">
              <p class="text-xs font-bold text-slate-500 uppercase">Server Time (UTC)</p>
              <h4 class="text-lg font-bold text-slate-900 mt-1">{{ h.serverTimeUtc | date:'mediumTime' }}</h4>
              <p class="text-xs text-slate-500 mt-2">{{ h.serverTimeUtc | date:'fullDate' }}</p>
            </div>
          </div>
        </div>
      </div>

      <!-- Modal: Assign Role -->
      <p-dialog
        header="Assign System Role"
        [(visible)]="displayAssignRoleDialog"
        [modal]="true"
        [style]="{ width: '400px' }"
        styleClass="p-fluid"
      >
        <div *ngIf="selectedUserForRole" class="flex flex-col gap-4 mt-2">
          <div class="p-3 bg-slate-50 border border-slate-200 rounded-lg">
            <div class="font-bold text-slate-900">{{ selectedUserForRole.firstName }} {{ selectedUserForRole.lastName }}</div>
            <div class="text-xs text-slate-500">{{ selectedUserForRole.email }}</div>
          </div>

          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-slate-700">Select Access Role *</label>
            <select [(ngModel)]="selectedRoleName" class="w-full p-2 border border-slate-300 rounded-md bg-white text-sm">
              <option value="Admin">Admin (Full System Access)</option>
              <option value="Doctor">Doctor (Clinical & Prescriptions)</option>
              <option value="Nurse">Nurse (Vitals & Patient Records)</option>
              <option value="Staff">Staff (Reception & Billing)</option>
            </select>
          </div>

          <div class="flex justify-end gap-2 mt-4">
            <p-button
              type="button"
              label="Cancel"
              styleClass="p-button-text p-button-secondary"
              (click)="displayAssignRoleDialog = false"
            ></p-button>
            <p-button
              type="button"
              label="Save Role"
              styleClass="p-button-primary"
              (click)="saveUserRole()"
              [disabled]="submitting()"
            ></p-button>
          </div>
        </div>
      </p-dialog>
    </div>
  `
})
export class AdminListComponent implements OnInit {
  private adminService = inject(AdminService);

  users = signal<UserAccount[]>([]);
  auditLogs = signal<AuditLogEntry[]>([]);
  health = signal<SystemHealth | null>(null);

  loading = signal<boolean>(false);
  submitting = signal<boolean>(false);

  activeTab = signal<'users' | 'audit' | 'system'>('users');
  auditFilter = signal<string | null>(null);

  displayAssignRoleDialog = false;
  selectedUserForRole: UserAccount | null = null;
  selectedRoleName = 'Staff';

  filteredAuditLogs = computed(() => {
    const list = this.auditLogs();
    const filter = this.auditFilter();
    if (!filter) return list;

    return list.filter(l => l.entityName.toLowerCase() === filter.toLowerCase());
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);

    this.adminService.getUsers().subscribe({
      next: (u) => {
        this.users.set(u);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });

    this.adminService.getAuditLogs().subscribe({
      next: (logs) => this.auditLogs.set(logs)
    });

    this.adminService.getSystemHealth().subscribe({
      next: (h) => this.health.set(h)
    });
  }

  setAuditFilter(entityName: string | null): void {
    this.auditFilter.set(entityName);
  }

  openAssignRoleDialog(user: UserAccount): void {
    this.selectedUserForRole = user;
    this.selectedRoleName = user.roles[0] || 'Staff';
    this.displayAssignRoleDialog = true;
  }

  saveUserRole(): void {
    if (!this.selectedUserForRole) return;

    this.submitting.set(true);
    this.adminService.updateUserRole(this.selectedUserForRole.id, this.selectedRoleName).subscribe({
      next: (updatedUser) => {
        this.users.update(list => list.map(u => u.id === updatedUser.id ? updatedUser : u));
        this.displayAssignRoleDialog = false;
        this.submitting.set(false);
        Swal.fire({
          title: 'Successful!',
          text: `Role for ${updatedUser.userName || updatedUser.email} updated to ${this.selectedRoleName}.`,
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      },
      error: () => this.submitting.set(false)
    });
  }

  toggleAccountStatus(user: UserAccount): void {
    const newStatus = !user.isActive;
    this.adminService.toggleUserStatus(user.id, newStatus).subscribe({
      next: (updatedUser) => {
        this.users.update(list => list.map(u => u.id === updatedUser.id ? updatedUser : u));
        Swal.fire({
          title: 'Successful!',
          text: `Account for ${updatedUser.userName || updatedUser.email} has been ${updatedUser.isActive ? 'enabled' : 'disabled'}.`,
          icon: 'success',
          confirmButtonColor: '#10b981',
          timer: 2500,
          timerProgressBar: true
        });
      }
    });
  }
}
