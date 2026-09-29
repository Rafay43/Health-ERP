import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

import { StaffService } from '../data-access/staff.service';

@Component({
    selector: 'app-staff-registration',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        RouterLink,
        CardModule,
        InputTextModule,
        ButtonModule,
        MessageModule,
    ],
    template: `
    <div class="max-w-2xl mx-auto">
      <div class="flex items-center gap-2 mb-4">
        <a routerLink="/staff" class="text-[var(--color-text-secondary)] hover:text-[var(--color-primary)] flex items-center gap-1 text-sm font-medium">
          <i class="pi pi-arrow-left"></i> Back to Staff Directory
        </a>
      </div>

      <p-card header="Add New Doctor / Staff Member">
        <form [formGroup]="staffForm" (ngSubmit)="onSubmit()" class="flex flex-col gap-4 mt-2">
          <div *ngIf="errorMessage()" class="mb-2">
            <p-message severity="error" [text]="errorMessage()!"></p-message>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-1">
              <label for="firstName" class="text-sm font-medium text-[var(--color-text-primary)]">First Name *</label>
              <input id="firstName" pInputText formControlName="firstName" placeholder="Sarah" />
            </div>
            <div class="flex flex-col gap-1">
              <label for="lastName" class="text-sm font-medium text-[var(--color-text-primary)]">Last Name *</label>
              <input id="lastName" pInputText formControlName="lastName" placeholder="Connor" />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-1">
              <label for="email" class="text-sm font-medium text-[var(--color-text-primary)]">Email Address *</label>
              <input id="email" type="email" pInputText formControlName="email" placeholder="sarah.connor@healtherp.com" />
            </div>
            <div class="flex flex-col gap-1">
              <label for="phoneNumber" class="text-sm font-medium text-[var(--color-text-primary)]">Phone Number</label>
              <input id="phoneNumber" pInputText formControlName="phoneNumber" placeholder="+1 555-0192" />
            </div>
          </div>

          <div class="grid grid-cols-3 gap-4">
            <div class="flex flex-col gap-1">
              <label for="specialty" class="text-sm font-medium text-[var(--color-text-primary)]">Specialty *</label>
              <input id="specialty" pInputText formControlName="specialty" placeholder="Cardiology" />
            </div>
            <div class="flex flex-col gap-1">
              <label for="licenseNumber" class="text-sm font-medium text-[var(--color-text-primary)]">License Number</label>
              <input id="licenseNumber" pInputText formControlName="licenseNumber" placeholder="MD-89210" />
            </div>
            <div class="flex flex-col gap-1">
              <label for="department" class="text-sm font-medium text-[var(--color-text-primary)]">Department *</label>
              <input id="department" pInputText formControlName="department" placeholder="Outpatient" />
            </div>
          </div>

          <div class="flex justify-end gap-2 mt-4">
            <p-button label="Cancel" icon="pi pi-times" [text]="true" routerLink="/staff"></p-button>
            <p-button
              type="submit"
              label="Save Staff Member"
              icon="pi pi-check"
              [loading]="isLoading()"
              [disabled]="staffForm.invalid || isLoading()"
            ></p-button>
          </div>
        </form>
      </p-card>
    </div>
  `,
})
export class StaffRegistration {
    private fb = inject(FormBuilder);
    private staffService = inject(StaffService);
    private router = inject(Router);

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);

    staffForm = this.fb.group({
        firstName: ['', [Validators.required]],
        lastName: ['', [Validators.required]],
        email: ['', [Validators.required, Validators.email]],
        phoneNumber: [''],
        specialty: ['', [Validators.required]],
        licenseNumber: [''],
        department: ['', [Validators.required]],
    });

    onSubmit(): void {
        if (this.staffForm.invalid) return;

        this.isLoading.set(true);
        this.errorMessage.set(null);

        const formValue = this.staffForm.value;

        this.staffService
            .createStaff({
                firstName: formValue.firstName!,
                lastName: formValue.lastName!,
                email: formValue.email!,
                phoneNumber: formValue.phoneNumber || '',
                specialty: formValue.specialty!,
                licenseNumber: formValue.licenseNumber || '',
                department: formValue.department!,
            })
            .subscribe({
                next: () => {
                    this.isLoading.set(false);
                    this.router.navigate(['/staff']);
                },
                error: (err) => {
                    this.isLoading.set(false);
                    this.errorMessage.set(err.error?.message || 'Failed to register staff member.');
                },
            });
    }
}
