import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';

import { StaffService } from './data-access/staff.service';
import { Staff } from './data-access/staff.model';

@Component({
  selector: 'app-staff-list',
  standalone: true,
  imports: [CommonModule, RouterLink, TableModule, ButtonModule, TagModule],
  template: `
    <div class="flex flex-col gap-4">
      <div class="flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Doctors & Staff Directory</h1>
          <p class="text-sm text-[var(--color-text-secondary)]">Manage clinical staff, specialties, and licenses</p>
        </div>
        <p-button
          label="Add Staff Member"
          icon="pi pi-user-plus"
          routerLink="/staff/register"
        ></p-button>
      </div>

      <div class="bg-[var(--color-surface)] rounded-lg border border-[var(--color-border)] p-4">
        <p-table [value]="staffMembers()" [loading]="isLoading()" [paginator]="true" [rows]="10">
          <ng-template pTemplate="header">
            <tr>
              <th>Name</th>
              <th>Specialty</th>
              <th>Department</th>
              <th>Email</th>
              <th>Phone</th>
              <th>License #</th>
              <th>Status</th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-member>
            <tr>
              <td class="font-semibold text-[var(--color-text-primary)]">{{ member.firstName }} {{ member.lastName }}</td>
              <td>{{ member.specialty }}</td>
              <td>{{ member.department }}</td>
              <td>{{ member.email }}</td>
              <td>{{ member.phoneNumber || '-' }}</td>
              <td>{{ member.licenseNumber || '-' }}</td>
              <td>
                <p-tag
                  [value]="member.isActive ? 'Active' : 'Inactive'"
                  [severity]="member.isActive ? 'success' : 'danger'"
                ></p-tag>
              </td>
            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="7" class="text-center py-6 text-[var(--color-text-secondary)]">
                No staff members found. Click "Add Staff Member" to register one.
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `,
})
export class StaffList implements OnInit {
  private staffService = inject(StaffService);

  staffMembers = signal<Staff[]>([]);
  isLoading = signal(true);

  ngOnInit(): void {
    this.loadStaff();
  }

  loadStaff(): void {
    this.isLoading.set(true);
    this.staffService.getStaff().subscribe({
      next: (data) => {
        this.staffMembers.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }
}
