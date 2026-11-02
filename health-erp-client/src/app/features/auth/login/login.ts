import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

import { AuthService } from '../../../core/services/auth.service';
import { PatientAccountService } from '../../../core/services/patient-account.service';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    MessageModule,
  ],
  template: `
    <div class="min-h-screen lg:h-screen lg:max-h-screen w-full flex bg-[#F4F7FA] font-sans antialiased text-[#1E293B] lg:overflow-hidden">
      
      <!-- LEFT COLUMN: Enterprise Login Form Section (50% on Desktop) -->
      <div class="w-full lg:w-1/2 h-full min-h-screen lg:min-h-0 flex flex-col justify-between p-6 sm:p-10 lg:p-12 bg-white relative z-10 overflow-y-auto lg:overflow-y-hidden shadow-sm lg:shadow-none">
        
        <!-- Top Brand Header -->
        <div class="flex items-center justify-between w-full max-w-[420px] mx-auto">
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-xl bg-[#0F766E] text-white flex items-center justify-center shadow-md shadow-[#0F766E]/20">
              <i class="pi pi-plus-circle text-xl"></i>
            </div>
            <div>
              <span class="text-lg font-bold text-[#0F172A] tracking-tight block leading-tight">Health ERP</span>
              <span class="text-xs font-medium text-[#64748B] block">Hospital Management System</span>
            </div>
          </div>
          
          <div class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-medium bg-slate-100 text-[#64748B] border border-slate-200">
            <span class="w-1.5 h-1.5 rounded-full bg-[#10B981] animate-pulse"></span>
            <span>System Operational</span>
          </div>
        </div>

        <!-- Centered Main Login Container (420px max width) -->
        <div class="w-full max-w-[420px] mx-auto my-auto pt-4 pb-2 lg:pt-6 lg:pb-3">
          
          <div class="mb-6">
            <h1 class="text-3xl lg:text-[32px] font-bold text-[#0F172A] tracking-tight mb-2 leading-tight">Welcome back</h1>
            <p class="text-sm font-normal text-[#64748B]">Sign in securely to access your hospital workspace.</p>
          </div>

          <form [formGroup]="loginForm" (ngSubmit)="onSubmit()" class="flex flex-col gap-5">
            
            <!-- Error Alert -->
            <div *ngIf="errorMessage()" class="mb-1">
              <p-message severity="error" [text]="errorMessage()!"></p-message>
            </div>

            <!-- Email Address Field -->
            <div class="flex flex-col gap-1.5">
              <label for="email" class="text-sm font-semibold text-[#1E293B]">Email Address</label>
              <div class="relative flex items-center">
                <i class="pi pi-envelope absolute left-4 text-slate-400 text-base z-10 pointer-events-none"></i>
                <input
                  id="email"
                  type="email"
                  pInputText
                  formControlName="email"
                  placeholder="name@hospital.com"
                  class="w-full !h-[50px] !pl-11 !py-3 !pr-4 text-sm font-medium rounded-[10px] border border-slate-300 focus:border-[#0F766E] focus:ring-2 focus:ring-[#0F766E]/20 transition-all duration-200 text-[#0F172A] bg-white"
                />
              </div>
              <div *ngIf="loginForm.get('email')?.touched && loginForm.get('email')?.invalid" class="text-xs text-[#DC2626] font-medium mt-1">
                Please enter a valid email address.
              </div>
            </div>

            <!-- Password Field with Show/Hide Toggle -->
            <div class="flex flex-col gap-1.5">
              <label for="password" class="text-sm font-semibold text-[#1E293B]">Password</label>
              <div class="relative flex items-center">
                <i class="pi pi-lock absolute left-4 text-slate-400 text-base z-10 pointer-events-none"></i>
                <p-password
                  id="password"
                  formControlName="password"
                  [feedback]="false"
                  [toggleMask]="true"
                  styleClass="w-full"
                  inputStyleClass="w-full !h-[50px] !pl-11 !py-3 !pr-10 text-sm font-medium rounded-[10px] border border-slate-300 focus:border-[#0F766E] focus:ring-2 focus:ring-[#0F766E]/20 transition-all duration-200 text-[#0F172A] bg-white"
                  placeholder="••••••••"
                ></p-password>
              </div>
              <div *ngIf="loginForm.get('password')?.touched && loginForm.get('password')?.invalid" class="text-xs text-[#DC2626] font-medium mt-1">
                Password is required.
              </div>
            </div>

            <!-- Remember Me & Forgot Password Row -->
            <div class="flex items-center justify-between my-0.5">
              <label class="flex items-center gap-2 cursor-pointer select-none">
                <input
                  type="checkbox"
                  class="w-4 h-4 rounded text-[#0F766E] border-slate-300 focus:ring-[#0F766E] cursor-pointer"
                />
                <span class="text-sm font-medium text-[#64748B]">Remember me</span>
              </label>
              <a routerLink="/forgot-password" class="text-sm font-semibold text-[#0F766E] hover:text-[#0D665F] hover:underline transition-colors">
                Forgot password?
              </a>
            </div>

            <!-- Primary Sign In Button -->
            <div class="mt-1 flex flex-col gap-4">
              <button
                type="submit"
                pButton
                label="Sign in to Health ERP"
                icon="pi pi-arrow-right"
                iconPos="right"
                [loading]="isLoading()"
                [disabled]="loginForm.invalid || isLoading()"
                class="w-full !h-[50px] !rounded-[10px] !bg-[#0F766E] hover:!bg-[#0D665F] !border-[#0F766E] text-white font-semibold text-sm shadow-md shadow-[#0F766E]/20 hover:-translate-y-0.5 transition-all duration-200 cursor-pointer flex items-center justify-center gap-2"
              ></button>

              <!-- Subtle Divider -->
              <div class="relative flex items-center justify-center my-1.5">
                <div class="border-t border-slate-200 w-full"></div>
                <span class="bg-white px-3 text-[11px] font-medium text-[#64748B] uppercase tracking-wider whitespace-nowrap absolute">
                  Quick demo access
                </span>
              </div>

              <!-- Quick Demo Access Buttons -->
              <div class="grid grid-cols-2 gap-3">
                <button
                  type="button"
                  class="py-2.5 px-3 rounded-lg text-xs font-semibold bg-slate-50 text-slate-700 border border-slate-200 shadow-2xs hover:bg-teal-50/70 hover:border-teal-300 hover:text-[#0F766E] transition-all duration-150 cursor-pointer flex items-center justify-center gap-2"
                  (click)="fillDemoCredentials('admin@healtherp.com', '')"
                >
                  <i class="pi pi-shield text-xs text-[#0F766E]"></i> Admin Demo
                </button>
                <button
                  type="button"
                  class="py-2.5 px-3 rounded-lg text-xs font-semibold bg-slate-50 text-slate-700 border border-slate-200 shadow-2xs hover:bg-teal-50/70 hover:border-teal-300 hover:text-[#0F766E] transition-all duration-150 cursor-pointer flex items-center justify-center gap-2"
                  (click)="fillDemoCredentials('doctor@healtherp.com', '')"
                >
                  <i class="pi pi-user text-xs text-teal-600"></i> Doctor Demo
                </button>
              </div>

              <!-- Create Account Link -->
              <div class="text-center text-sm font-medium text-[#64748B] mt-2">
                Don't have an account?
                <a routerLink="/register" class="text-[#0F766E] hover:text-[#0D665F] font-semibold ml-1 hover:underline transition-colors">
                  Create account
                </a>
              </div>
            </div>

          </form>
        </div>

        <!-- Security / Trust Footer -->
        <div class="text-xs text-[#64748B] font-medium flex items-center justify-between border-t border-slate-200/80 pt-4 max-w-[420px] mx-auto w-full">
          <span class="flex items-center gap-1.5">
            <i class="pi pi-lock text-slate-400 text-xs"></i> Secure encrypted access &bull; Role-based permissions
          </span>
          <span>&copy; {{ currentYear }} Health ERP</span>
        </div>

      </div>

      <!-- RIGHT COLUMN: Clean Healthcare Enterprise Visual Panel (50% on Desktop) -->
      <div class="hidden lg:flex lg:w-1/2 h-full bg-[#071A2B] relative flex-col justify-between p-8 lg:p-12 overflow-hidden text-white">
        
        <!-- Abstract Medical Vector Pattern Overlay -->
        <div class="absolute inset-0 opacity-10 pointer-events-none">
          <svg class="w-full h-full" xmlns="http://www.w3.org/2000/svg">
            <defs>
              <pattern id="medical-grid" width="40" height="40" patternUnits="userSpaceOnUse">
                <circle cx="20" cy="20" r="1" fill="#FFFFFF" />
                <path d="M 20 15 L 20 25 M 15 20 L 25 20" stroke="#FFFFFF" stroke-width="0.5" />
              </pattern>
            </defs>
            <rect width="100%" height="100%" fill="url(#medical-grid)" />
            <path d="M 0 300 Q 150 240, 300 300 T 600 300 T 900 300" fill="none" stroke="#0F766E" stroke-width="2" opacity="0.3" />
            <path d="M 0 500 Q 200 420, 400 500 T 800 500" fill="none" stroke="#38BDF8" stroke-width="1.5" opacity="0.2" />
          </svg>
        </div>

        <!-- Subtle Ambient Glows -->
        <div class="absolute top-0 right-0 w-96 h-96 bg-[#0F766E]/20 rounded-full blur-[100px] pointer-events-none"></div>
        <div class="absolute bottom-0 left-0 w-96 h-96 bg-[#38BDF8]/10 rounded-full blur-[100px] pointer-events-none"></div>

        <!-- Upper Content Section -->
        <div class="relative z-10 max-w-lg">
          <div class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-[#0F766E]/20 text-[#CCFBF1] border border-[#0F766E]/30 mb-3">
            <i class="pi pi-shield text-xs"></i> Secure Hospital Platform
          </div>

          <h2 class="text-2xl lg:text-3xl font-bold tracking-tight text-white mb-2 leading-snug">
            Smarter hospital operations, all in one place.
          </h2>

          <p class="text-sm text-[#94A3B8] font-normal leading-relaxed">
            Manage patients, clinical workflows, beds, billing and hospital operations through one secure platform.
          </p>
        </div>

        <!-- Center Decorative Hospital Dashboard Mockup Card -->
        <div class="relative z-10 my-3 lg:my-3.5 max-w-lg bg-slate-900/85 border border-slate-700/60 rounded-2xl p-4 shadow-2xl backdrop-blur-md transform scale-[0.98] origin-center">
          
          <!-- Simulated Mockup Header -->
          <div class="flex items-center justify-between pb-2 border-b border-slate-800 mb-3">
            <div class="flex items-center gap-2">
              <div class="w-2.5 h-2.5 rounded-full bg-rose-500/80"></div>
              <div class="w-2.5 h-2.5 rounded-full bg-amber-500/80"></div>
              <div class="w-2.5 h-2.5 rounded-full bg-emerald-500/80"></div>
              <span class="text-xs font-semibold text-slate-300 ml-1.5">Health ERP Operations Hub</span>
            </div>
            <span class="text-[11px] font-medium bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 px-2 py-0.5 rounded-full flex items-center gap-1">
              <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span> Live Sync
            </span>
          </div>

          <!-- 2x2 Clean Metric Cards Grid -->
          <div class="grid grid-cols-2 gap-2.5 mb-3">
            <div class="bg-slate-800/70 border border-slate-700/50 rounded-xl p-3">
              <span class="text-xs font-medium text-slate-300 block mb-0.5">Patient Admissions</span>
              <div class="flex items-baseline justify-between">
                <span class="text-xl font-bold text-white">124</span>
                <span class="text-[11px] font-semibold text-emerald-400 bg-emerald-500/10 px-1.5 py-0.5 rounded">+12%</span>
              </div>
            </div>

            <div class="bg-slate-800/70 border border-slate-700/50 rounded-xl p-3">
              <span class="text-xs font-medium text-slate-300 block mb-0.5">Bed Occupancy</span>
              <div class="flex items-baseline justify-between">
                <span class="text-xl font-bold text-white">78%</span>
                <span class="text-[11px] font-medium text-slate-300">340/436</span>
              </div>
              <div class="w-full bg-slate-700/60 rounded-full h-1.5 mt-1.5 overflow-hidden">
                <div class="bg-[#0F766E] h-1.5 rounded-full" style="width: 78%"></div>
              </div>
            </div>

            <div class="bg-slate-800/70 border border-slate-700/50 rounded-xl p-3">
              <span class="text-xs font-medium text-slate-300 block mb-0.5">ER Wait Time</span>
              <div class="flex items-baseline justify-between">
                <span class="text-xl font-bold text-white">11 min</span>
                <span class="text-[11px] font-semibold text-emerald-400 bg-emerald-500/10 px-1.5 py-0.5 rounded">-4 min</span>
              </div>
            </div>

            <div class="bg-slate-800/70 border border-slate-700/50 rounded-xl p-3">
              <span class="text-xs font-medium text-slate-300 block mb-0.5">Billing Today</span>
              <div class="flex items-baseline justify-between">
                <span class="text-xl font-bold text-white">$24.8K</span>
                <i class="pi pi-chart-line text-sky-400 text-xs"></i>
              </div>
            </div>
          </div>

          <!-- Mini Activity Indicator Bar -->
          <div class="bg-slate-800/50 border border-slate-700/40 rounded-xl p-2.5 flex items-center justify-between">
            <div class="flex items-center gap-2">
              <div class="w-7 h-7 rounded-lg bg-[#0F766E]/20 text-[#CCFBF1] flex items-center justify-center">
                <i class="pi pi-check-circle text-xs"></i>
              </div>
              <div>
                <span class="text-xs font-semibold text-slate-200 block leading-tight">Bed Allocation Automated</span>
                <span class="text-[11px] font-normal text-slate-300 block">Ward B &bull; 4 beds assigned</span>
              </div>
            </div>
            <span class="text-[11px] font-medium text-slate-400">2m ago</span>
          </div>

        </div>

        <!-- Bottom 3 Compact Benefit Items -->
        <div class="relative z-10 flex flex-wrap items-center justify-between gap-3 border-t border-slate-800/80 pt-3.5 max-w-lg text-xs font-medium text-slate-300">
          <div class="flex items-center gap-2">
            <div class="w-4.5 h-4.5 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[10px]">
              ✓
            </div>
            <span>Real-time clinical insights</span>
          </div>
          <div class="flex items-center gap-2">
            <div class="w-4.5 h-4.5 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[10px]">
              ✓
            </div>
            <span>Secure role-based access</span>
          </div>
          <div class="flex items-center gap-2">
            <div class="w-4.5 h-4.5 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[10px]">
              ✓
            </div>
            <span>Centralized hospital operations</span>
          </div>
        </div>

      </div>

    </div>
  `,
})
export class Login {

  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private patientAccountService = inject(PatientAccountService);
  private router = inject(Router);

  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  currentYear = new Date().getFullYear();

  loginForm = this.fb.group({
    email: ['admin@healtherp.com', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  fillDemoCredentials(email: string, pass: string): void {
    this.loginForm.patchValue({ email, password: pass });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { email, password } = this.loginForm.value;

    this.authService.login({ email: email!, password: password! }).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        Swal.fire({
          title: 'Successful!',
          text: `Welcome back, ${res?.firstName || 'User'}!`,
          icon: 'success',
          confirmButtonColor: '#0f766e',
          timer: 2000,
          timerProgressBar: true
        });
        if (this.authService.isPatient()) {
          this.patientAccountService.getStatus().subscribe({
            next: (status) => {
              if (status.isLinked) {
                this.router.navigate(['/portal']);
              } else {
                this.router.navigate(['/portal-activation']);
              }
            },
            error: () => this.router.navigate(['/portal-activation']),
          });
        } else {
          this.router.navigate([this.authService.getStaffHomeRoute()]);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Invalid email or password.');
      },
    });
  }
}




