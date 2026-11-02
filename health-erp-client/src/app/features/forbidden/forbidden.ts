import { Component, inject } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-forbidden',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <div class="min-h-screen w-full flex bg-[#F8FAFC] font-sans antialiased text-[#1E293B] items-center justify-center p-6">
      
      <div class="w-full max-w-md bg-white border border-slate-200 rounded-2xl p-8 text-center shadow-lg">
        
        <!-- Icon Badge -->
        <div class="w-16 h-16 rounded-2xl bg-amber-50 border border-amber-200 text-amber-600 flex items-center justify-center mx-auto mb-5 shadow-xs">
          <i class="pi pi-lock text-3xl"></i>
        </div>

        <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-amber-50 text-amber-700 border border-amber-200 mb-4">
          <i class="pi pi-shield text-xs"></i> 403 Forbidden
        </span>

        <h1 class="text-2xl font-bold text-[#0F172A] tracking-tight mb-2">Access Restricted</h1>
        
        <p class="text-sm text-[#64748B] font-normal leading-relaxed mb-6">
          You don't have permission to access this area of Health ERP. If you believe you should have access, please contact your hospital system administrator.
        </p>

        <div class="flex flex-col sm:flex-row gap-3">
          <button
            type="button"
            (click)="goBack()"
            class="w-full sm:w-1/2 h-11 rounded-xl bg-slate-100 hover:bg-slate-200 text-[#0F172A] font-semibold text-xs transition-colors cursor-pointer flex items-center justify-center gap-2"
          >
            <i class="pi pi-arrow-left text-xs"></i> Go Back
          </button>

          <button
            type="button"
            (click)="goToDashboard()"
            class="w-full sm:w-1/2 h-11 rounded-xl bg-[#0F766E] hover:bg-[#0D665F] text-white font-semibold text-xs transition-colors cursor-pointer flex items-center justify-center gap-2 shadow-xs"
          >
            <i class="pi pi-home text-xs"></i> Dashboard
          </button>
        </div>

      </div>

    </div>
  `,
})
export class ForbiddenComponent {
  private location = inject(Location);
  private router = inject(Router);
  private authService = inject(AuthService);

  goBack(): void {
    this.location.back();
  }

  goToDashboard(): void {
    if (this.authService.isPatient()) {
      this.router.navigate(['/portal-coming-soon']);
    } else {
      this.router.navigate(['/dashboard']);
    }
  }
}
