import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { PatientPortalService, PatientPortalProfileDto } from '../../core/services/patient-portal.service';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-patient-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="min-h-screen bg-[#F8FAFC] font-sans antialiased text-[#0F172A] flex flex-col md:flex-row">

      <!-- MOBILE HEADER BAR -->
      <header class="md:hidden bg-white border-b border-slate-200 px-4 py-3 flex items-center justify-between sticky top-0 z-40 shadow-xs">
        <div class="flex items-center gap-2.5">
          <div class="w-8 h-8 rounded-lg bg-[#0F766E] text-white flex items-center justify-center font-bold text-sm shadow-xs">
            <i class="pi pi-plus-circle"></i>
          </div>
          <div>
            <span class="text-sm font-bold text-[#0F172A] block leading-tight">Health ERP</span>
            <span class="text-[10px] font-medium text-[#64748B] block">Patient Portal</span>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <button (click)="toggleMobileMenu()" class="p-2 rounded-lg text-slate-600 hover:bg-slate-100 transition-colors" aria-label="Toggle Navigation">
            <i class="pi" [ngClass]="isMobileMenuOpen() ? 'pi-times' : 'pi-bars'"></i>
          </button>
        </div>
      </header>

      <!-- DESKTOP & MOBILE NAVIGATION SIDEBAR -->
      <aside
        class="fixed inset-y-0 left-0 z-50 w-64 bg-white border-r border-slate-200 flex flex-col justify-between transition-transform duration-200 ease-in-out md:static md:translate-x-0"
        [ngClass]="isMobileMenuOpen() ? 'translate-x-0' : '-translate-x-full md:translate-x-0'"
      >
        <!-- Sidebar Top: Brand & Patient Card -->
        <div>
          <!-- Desktop Brand Logo -->
          <div class="hidden md:flex items-center gap-3 p-6 border-b border-slate-100">
            <div class="w-10 h-10 rounded-xl bg-[#0F766E] text-white flex items-center justify-center shadow-md shadow-[#0F766E]/20">
              <i class="pi pi-plus-circle text-xl"></i>
            </div>
            <div>
              <span class="text-base font-bold text-[#0F172A] block leading-tight">Health ERP</span>
              <span class="text-xs font-medium text-[#0F766E] block">Patient Portal</span>
            </div>
          </div>

          <!-- Patient Summary Badge -->
          <div class="p-4 mx-3 my-3 bg-teal-50/60 border border-teal-100/80 rounded-xl flex items-center gap-3">
            <div class="w-10 h-10 rounded-full bg-[#0F766E] text-white font-semibold text-sm flex items-center justify-center shrink-0 shadow-xs">
              {{ getInitials() }}
            </div>
            <div class="overflow-hidden">
              <span class="text-xs font-semibold text-[#0F172A] block truncate leading-tight">
                {{ profile()?.displayName || currentUser()?.firstName || 'Patient' }}
              </span>
              <span class="text-[11px] font-medium text-[#64748B] block truncate">
                {{ profile()?.mrn ? 'MRN: ' + profile()?.mrn : 'Patient Account' }}
              </span>
            </div>
          </div>

          <!-- Navigation Links -->
          <nav class="px-3 py-2 space-y-1">
            <a
              routerLink="/portal"
              [routerLinkActiveOptions]="{ exact: true }"
              routerLinkActive="bg-[#0F766E]/10 text-[#0F766E] font-semibold"
              (click)="closeMobileMenu()"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[#475569] hover:bg-slate-100/80 hover:text-[#0F172A] transition-colors"
            >
              <i class="pi pi-home text-base w-5 text-center"></i>
              <span>Overview</span>
            </a>

            <a
              routerLink="/portal/appointments"
              routerLinkActive="bg-[#0F766E]/10 text-[#0F766E] font-semibold"
              (click)="closeMobileMenu()"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[#475569] hover:bg-slate-100/80 hover:text-[#0F172A] transition-colors"
            >
              <i class="pi pi-calendar text-base w-5 text-center"></i>
              <span>My Appointments</span>
            </a>

            <a
              routerLink="/portal/health-record"
              routerLinkActive="bg-[#0F766E]/10 text-[#0F766E] font-semibold"
              (click)="closeMobileMenu()"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[#475569] hover:bg-slate-100/80 hover:text-[#0F172A] transition-colors"
            >
              <i class="pi pi-heart text-base w-5 text-center"></i>
              <span>My Health Record</span>
            </a>

            <a
              routerLink="/portal/prescriptions"
              routerLinkActive="bg-[#0F766E]/10 text-[#0F766E] font-semibold"
              (click)="closeMobileMenu()"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[#475569] hover:bg-slate-100/80 hover:text-[#0F172A] transition-colors"
            >
              <i class="pi pi-[#0F766E] pi-sliders-h text-base w-5 text-center"></i>
              <span>My Prescriptions</span>
            </a>

            <a
              routerLink="/portal/lab-results"
              routerLinkActive="bg-[#0F766E]/10 text-[#0F766E] font-semibold"
              (click)="closeMobileMenu()"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[#475569] hover:bg-slate-100/80 hover:text-[#0F172A] transition-colors"
            >
              <i class="pi pi-file text-base w-5 text-center"></i>
              <span>My Lab Results</span>
            </a>

            <a
              routerLink="/portal/billing"
              routerLinkActive="bg-[#0F766E]/10 text-[#0F766E] font-semibold"
              (click)="closeMobileMenu()"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[#475569] hover:bg-slate-100/80 hover:text-[#0F172A] transition-colors"
            >
              <i class="pi pi-wallet text-base w-5 text-center"></i>
              <span>My Billing</span>
            </a>

            <a
              routerLink="/portal/profile"
              routerLinkActive="bg-[#0F766E]/10 text-[#0F766E] font-semibold"
              (click)="closeMobileMenu()"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[#475569] hover:bg-slate-100/80 hover:text-[#0F172A] transition-colors"
            >
              <i class="pi pi-user text-base w-5 text-center"></i>
              <span>My Profile</span>
            </a>
          </nav>
        </div>

        <!-- Sidebar Bottom Actions -->
        <div class="p-3 border-t border-slate-100 space-y-1">
          <button
            (click)="showSupportModal()"
            class="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[#64748B] hover:bg-slate-100 hover:text-[#0F172A] transition-colors cursor-pointer"
          >
            <i class="pi pi-question-circle text-base w-5 text-center"></i>
            <span>Help & Support</span>
          </button>

          <button
            (click)="logout()"
            class="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-rose-600 hover:bg-rose-50 transition-colors cursor-pointer"
          >
            <i class="pi pi-sign-out text-base w-5 text-center"></i>
            <span>Sign Out</span>
          </button>
        </div>
      </aside>

      <!-- MOBILE OVERLAY BACKDROP -->
      <div
        *ngIf="isMobileMenuOpen()"
        (click)="closeMobileMenu()"
        class="fixed inset-0 bg-slate-900/40 z-40 md:hidden backdrop-blur-xs"
      ></div>

      <!-- MAIN PATIENT CONTENT AREA -->
      <div class="flex-1 flex flex-col min-w-0">

        <!-- TOP PATIENT DESKTOP HEADER -->
        <header class="hidden md:flex items-center justify-between h-16 bg-white border-b border-slate-200 px-8 sticky top-0 z-30 shadow-xs">
          <div class="flex items-center gap-3">
            <h1 class="text-lg font-bold text-[#0F172A] tracking-tight">{{ currentPageTitle() }}</h1>
            <span class="px-2.5 py-0.5 rounded-full text-xs font-medium bg-teal-50 text-[#0F766E] border border-teal-200/60">
              Patient Portal
            </span>
          </div>

          <div class="flex items-center gap-4">
            <button (click)="showSupportModal()" class="text-xs font-semibold text-[#64748B] hover:text-[#0F766E] flex items-center gap-1.5 transition-colors cursor-pointer">
              <i class="pi pi-question-circle text-sm"></i> Assistance
            </button>

            <div class="h-4 w-px bg-slate-200"></div>

            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-full bg-[#0F766E] text-white font-semibold text-xs flex items-center justify-center">
                {{ getInitials() }}
              </div>
              <div class="text-left">
                <span class="text-xs font-bold text-[#0F172A] block leading-tight">
                  {{ profile()?.displayName || currentUser()?.firstName || 'Patient' }}
                </span>
                <span class="text-[10px] font-medium text-[#64748B] block">Patient</span>
              </div>
            </div>

            <button (click)="logout()" class="p-2 rounded-lg text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-colors cursor-pointer" title="Sign Out">
              <i class="pi pi-sign-out text-base"></i>
            </button>
          </div>
        </header>

        <!-- ROUTER OUTLET CONTAINER -->
        <main class="flex-1 p-4 sm:p-6 lg:p-8 max-w-7xl w-full mx-auto">
          <router-outlet></router-outlet>
        </main>
      </div>

    </div>
  `,
})
export class PatientPortalShellComponent implements OnInit {
  private authService = inject(AuthService);
  private portalService = inject(PatientPortalService);
  private router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  profile = signal<PatientPortalProfileDto | null>(null);
  isMobileMenuOpen = signal(false);
  currentPageTitle = signal('Overview');

  ngOnInit(): void {
    this.loadProfile();
    this.updateTitleFromRoute(this.router.url);

    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.updateTitleFromRoute(event.urlAfterRedirects);
      });
  }

  loadProfile(): void {
    this.portalService.getProfile().subscribe({
      next: (data) => this.profile.set(data),
      error: () => {},
    });
  }

  getInitials(): string {
    const prof = this.profile();
    if (prof?.firstName && prof?.lastName) {
      return (prof.firstName[0] + prof.lastName[0]).toUpperCase();
    }
    const user = this.currentUser();
    if (user?.firstName) {
      return user.firstName[0].toUpperCase();
    }
    return 'P';
  }

  toggleMobileMenu(): void {
    this.isMobileMenuOpen.set(!this.isMobileMenuOpen());
  }

  closeMobileMenu(): void {
    this.isMobileMenuOpen.set(false);
  }

  showSupportModal(): void {
    Swal.fire({
      title: 'Hospital Patient Support',
      html: `
        <div class="text-left text-sm space-y-3 color-[#334155]">
          <p>For urgent medical assistance or emergency response, please call emergency services immediately.</p>
          <div class="p-3 bg-slate-50 border border-slate-200 rounded-lg space-y-1">
            <p class="font-semibold text-[#0F172A]">Hospital Helpline:</p>
            <p class="text-[#0F766E] font-medium">+1 (800) 555-HEALTH</p>
            <p class="text-xs text-slate-500">Available 24/7 for patient inquiries</p>
          </div>
        </div>
      `,
      icon: 'info',
      confirmButtonColor: '#0F766E',
      confirmButtonText: 'Close',
    });
  }

  logout(): void {
    this.authService.logout();
  }

  private updateTitleFromRoute(url: string): void {
    if (url.includes('/portal/appointments')) {
      this.currentPageTitle.set('My Appointments');
    } else if (url.includes('/portal/health-record')) {
      this.currentPageTitle.set('My Health Record');
    } else if (url.includes('/portal/prescriptions')) {
      this.currentPageTitle.set('My Prescriptions');
    } else if (url.includes('/portal/lab-results')) {
      this.currentPageTitle.set('My Lab Results');
    } else if (url.includes('/portal/billing')) {
      this.currentPageTitle.set('My Billing');
    } else if (url.includes('/portal/profile')) {
      this.currentPageTitle.set('My Profile');
    } else {
      this.currentPageTitle.set('Overview');
    }
  }
}
