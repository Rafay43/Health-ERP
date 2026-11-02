import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PatientPortalService, PatientPortalProfileDto } from '../../../core/services/patient-portal.service';

@Component({
  selector: 'app-portal-profile',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="space-y-6 max-w-4xl">

      <!-- PAGE HEADER -->
      <div>
        <h2 class="text-2xl font-bold text-[#0F172A] tracking-tight">My Profile</h2>
        <p class="text-sm text-[#64748B] mt-0.5">Read-only view of your hospital patient account details.</p>
      </div>

      <!-- LOADING SKELETON -->
      <div *ngIf="isLoading()" class="bg-white p-8 rounded-2xl border border-slate-200 animate-pulse space-y-4">
        <div class="h-16 w-16 bg-slate-200 rounded-full"></div>
        <div class="h-6 bg-slate-200 rounded w-1/3"></div>
      </div>

      <!-- PROFILE CARD -->
      <div *ngIf="!isLoading() && profile() as prof" class="space-y-6">

        <!-- HEADER AVATAR CARD -->
        <div class="bg-white p-6 sm:p-8 rounded-2xl border border-slate-200/80 shadow-xs flex flex-col sm:flex-row items-start sm:items-center gap-6">
          <div class="w-20 h-20 rounded-full bg-[#0F766E] text-white font-extrabold text-2xl flex items-center justify-center shrink-0 shadow-md shadow-[#0F766E]/20">
            {{ getInitials(prof) }}
          </div>

          <div class="space-y-1">
            <div class="flex items-center gap-3">
              <h3 class="text-xl sm:text-2xl font-bold text-[#0F172A]">{{ prof.displayName }}</h3>
              <span class="px-3 py-0.5 rounded-full text-xs font-semibold bg-teal-50 text-[#0F766E] border border-teal-200/60">
                Verified Patient
              </span>
            </div>
            <p class="text-xs font-semibold text-[#0F766E]">
              Medical Record Number: <span class="text-[#0F172A] font-bold">{{ prof.mrn }}</span>
            </p>
            <p class="text-xs text-slate-400 font-medium">
              Registered patient account linked to Health ERP System.
            </p>
          </div>
        </div>

        <!-- DETAILS GRID -->
        <div class="bg-white p-6 sm:p-8 rounded-2xl border border-slate-200/80 shadow-xs space-y-6">
          <h4 class="text-sm font-bold uppercase tracking-wider text-slate-400 border-b border-slate-100 pb-3">
            Personal & Identification Info
          </h4>

          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6 text-sm">

            <div class="bg-slate-50/60 p-4 rounded-xl border border-slate-200/50">
              <span class="text-xs text-slate-400 font-medium block mb-1">First Name</span>
              <span class="font-bold text-[#0F172A]">{{ prof.firstName }}</span>
            </div>

            <div class="bg-slate-50/60 p-4 rounded-xl border border-slate-200/50">
              <span class="text-xs text-slate-400 font-medium block mb-1">Last Name</span>
              <span class="font-bold text-[#0F172A]">{{ prof.lastName }}</span>
            </div>

            <div class="bg-slate-50/60 p-4 rounded-xl border border-slate-200/50">
              <span class="text-xs text-slate-400 font-medium block mb-1">Medical Record Number (MRN)</span>
              <span class="font-bold text-[#0F766E]">{{ prof.mrn }}</span>
            </div>

            <div class="bg-slate-50/60 p-4 rounded-xl border border-slate-200/50">
              <span class="text-xs text-slate-400 font-medium block mb-1">Date of Birth</span>
              <span class="font-bold text-[#0F172A]">{{ prof.dateOfBirth | date:'mediumDate' }}</span>
            </div>

            <div class="bg-slate-50/60 p-4 rounded-xl border border-slate-200/50">
              <span class="text-xs text-slate-400 font-medium block mb-1">Age</span>
              <span class="font-bold text-[#0F172A]">{{ prof.age }} years</span>
            </div>

            <div class="bg-slate-50/60 p-4 rounded-xl border border-slate-200/50">
              <span class="text-xs text-slate-400 font-medium block mb-1">Gender</span>
              <span class="font-bold text-[#0F172A]">{{ prof.gender || 'Not specified' }}</span>
            </div>

            <div class="bg-slate-50/60 p-4 rounded-xl border border-slate-200/50">
              <span class="text-xs text-slate-400 font-medium block mb-1">Phone Number</span>
              <span class="font-bold text-[#0F172A]">{{ prof.phoneNumber || 'Not on file' }}</span>
            </div>

            <div class="bg-slate-50/60 p-4 rounded-xl border border-slate-200/50 sm:col-span-2">
              <span class="text-xs text-slate-400 font-medium block mb-1">Registered Email Address</span>
              <span class="font-bold text-[#0F172A]">{{ prof.email || 'Not on file' }}</span>
            </div>

          </div>
        </div>

        <!-- PRIVACY NOTE FOOTER -->
        <div class="p-4 bg-teal-50/50 rounded-xl border border-teal-100 text-xs text-slate-600 flex items-start gap-2">
          <i class="pi pi-lock text-[#0F766E] text-sm mt-0.5 shrink-0"></i>
          <span>
            <strong>Privacy & Security Notice:</strong> To request corrections to your official medical record or contact information, please reach out to the hospital administration registrar.
          </span>
        </div>

      </div>

    </div>
  `,
})
export class PortalProfileComponent implements OnInit {
  private portalService = inject(PatientPortalService);

  isLoading = signal(true);
  profile = signal<PatientPortalProfileDto | null>(null);

  ngOnInit(): void {
    this.loadProfile();
  }

  loadProfile(): void {
    this.isLoading.set(true);
    this.portalService.getProfile().subscribe({
      next: (data) => {
        this.profile.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }

  getInitials(prof: PatientPortalProfileDto): string {
    if (prof.firstName && prof.lastName) {
      return (prof.firstName[0] + prof.lastName[0]).toUpperCase();
    }
    return 'P';
  }
}
