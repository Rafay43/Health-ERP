import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

import { AuthService } from '../../../core/services/auth.service';
import Swal from 'sweetalert2';

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password');
  const confirmPassword = control.get('confirmPassword');
  if (password && confirmPassword && password.value !== confirmPassword.value) {
    return { passwordMismatch: true };
  }
  return null;
}

@Component({
  selector: 'app-register',
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
      
      <!-- LEFT COLUMN: Enterprise Registration Form Section (50% on Desktop) -->
      <div class="w-full lg:w-1/2 h-full min-h-screen lg:min-h-0 flex flex-col justify-between p-6 sm:p-10 lg:p-12 bg-white relative z-10 overflow-y-auto shadow-sm lg:shadow-none">
        
        <!-- Top Brand Header -->
        <div class="flex items-center justify-between w-full max-w-[440px] mx-auto">
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

        <!-- Centered Main Form Container (440px max width) -->
        <div class="w-full max-w-[440px] mx-auto my-auto pt-4 pb-2 lg:pt-4 lg:pb-2">
          
          <div class="mb-5">
            <h1 class="text-3xl lg:text-[32px] font-bold text-[#0F172A] tracking-tight mb-1.5 leading-tight">Create your account</h1>
            <p class="text-sm font-normal text-[#64748B]">Set up your Health ERP account to access your hospital workspace.</p>
          </div>

          <form [formGroup]="registerForm" (ngSubmit)="onSubmit()" class="flex flex-col gap-4">
            
            <!-- Error Alert -->
            <div *ngIf="errorMessage()" class="mb-1">
              <p-message severity="error">{{ errorMessage()! }}</p-message>
            </div>

            <!-- Two-column First Name | Last Name Grid -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <!-- First Name -->
              <div class="flex flex-col gap-1.5">
                <label for="firstName" class="text-xs font-semibold text-[#1E293B]">First Name *</label>
                <div class="relative flex items-center">
                  <i class="pi pi-user absolute left-3.5 text-slate-400 text-sm z-10 pointer-events-none"></i>
                  <input
                    id="firstName"
                    type="text"
                    pInputText
                    formControlName="firstName"
                    autocomplete="given-name"
                    placeholder="John"
                    class="w-full !h-[46px] !pl-10 !py-2.5 !pr-3 text-sm font-medium rounded-[10px] border border-slate-300 focus:border-[#0F766E] focus:ring-2 focus:ring-[#0F766E]/20 transition-all duration-200 text-[#0F172A] bg-white"
                  />
                </div>
                <div *ngIf="registerForm.get('firstName')?.touched && registerForm.get('firstName')?.invalid" class="text-xs text-[#DC2626] font-medium mt-0.5">
                  First name is required.
                </div>
              </div>

              <!-- Last Name -->
              <div class="flex flex-col gap-1.5">
                <label for="lastName" class="text-xs font-semibold text-[#1E293B]">Last Name *</label>
                <div class="relative flex items-center">
                  <i class="pi pi-user absolute left-3.5 text-slate-400 text-sm z-10 pointer-events-none"></i>
                  <input
                    id="lastName"
                    type="text"
                    pInputText
                    formControlName="lastName"
                    autocomplete="family-name"
                    placeholder="Doe"
                    class="w-full !h-[46px] !pl-10 !py-2.5 !pr-3 text-sm font-medium rounded-[10px] border border-slate-300 focus:border-[#0F766E] focus:ring-2 focus:ring-[#0F766E]/20 transition-all duration-200 text-[#0F172A] bg-white"
                  />
                </div>
                <div *ngIf="registerForm.get('lastName')?.touched && registerForm.get('lastName')?.invalid" class="text-xs text-[#DC2626] font-medium mt-0.5">
                  Last name is required.
                </div>
              </div>
            </div>

            <!-- Email Address Field -->
            <div class="flex flex-col gap-1.5">
              <label for="email" class="text-xs font-semibold text-[#1E293B]">Email Address *</label>
              <div class="relative flex items-center">
                <i class="pi pi-envelope absolute left-3.5 text-slate-400 text-sm z-10 pointer-events-none"></i>
                <input
                  id="email"
                  type="email"
                  pInputText
                  formControlName="email"
                  autocomplete="email"
                  placeholder="demo.user@example.com"
                  class="w-full !h-[46px] !pl-10 !py-2.5 !pr-3 text-sm font-medium rounded-[10px] border border-slate-300 focus:border-[#0F766E] focus:ring-2 focus:ring-[#0F766E]/20 transition-all duration-200 text-[#0F172A] bg-white"
                />
              </div>
              <div *ngIf="registerForm.get('email')?.touched && registerForm.get('email')?.invalid" class="text-xs text-[#DC2626] font-medium mt-0.5">
                <span *ngIf="registerForm.get('email')?.hasError('required')">Email address is required.</span>
                <span *ngIf="registerForm.get('email')?.hasError('email')">Please enter a valid email address.</span>
              </div>
            </div>

            <!-- Password Field -->
            <div class="flex flex-col gap-1.5">
              <label for="password" class="text-xs font-semibold text-[#1E293B]">Password *</label>
              <div class="relative flex items-center">
                <i class="pi pi-lock absolute left-3.5 text-slate-400 text-sm z-10 pointer-events-none"></i>
                <p-password
                  id="password"
                  formControlName="password"
                  [feedback]="false"
                  [toggleMask]="true"
                  styleClass="w-full"
                  inputStyleClass="w-full !h-[46px] !pl-10 !py-2.5 !pr-9 text-sm font-medium rounded-[10px] border border-slate-300 focus:border-[#0F766E] focus:ring-2 focus:ring-[#0F766E]/20 transition-all duration-200 text-[#0F172A] bg-white"
                  placeholder="••••••••"
                  autocomplete="new-password"
                ></p-password>
              </div>
              <div *ngIf="registerForm.get('password')?.touched && registerForm.get('password')?.invalid" class="text-xs text-[#DC2626] font-medium mt-0.5">
                <span *ngIf="registerForm.get('password')?.hasError('required')">Password is required.</span>
                <span *ngIf="registerForm.get('password')?.hasError('minlength')">Password must be at least 8 characters.</span>
              </div>
            </div>

            <!-- Confirm Password Field -->
            <div class="flex flex-col gap-1.5">
              <label for="confirmPassword" class="text-xs font-semibold text-[#1E293B]">Confirm Password *</label>
              <div class="relative flex items-center">
                <i class="pi pi-shield absolute left-3.5 text-slate-400 text-sm z-10 pointer-events-none"></i>
                <p-password
                  id="confirmPassword"
                  formControlName="confirmPassword"
                  [feedback]="false"
                  [toggleMask]="true"
                  styleClass="w-full"
                  inputStyleClass="w-full !h-[46px] !pl-10 !py-2.5 !pr-9 text-sm font-medium rounded-[10px] border border-slate-300 focus:border-[#0F766E] focus:ring-2 focus:ring-[#0F766E]/20 transition-all duration-200 text-[#0F172A] bg-white"
                  placeholder="••••••••"
                  autocomplete="new-password"
                ></p-password>
              </div>
              <div *ngIf="(registerForm.get('confirmPassword')?.touched || registerForm.touched) && registerForm.hasError('passwordMismatch')" class="text-xs text-[#DC2626] font-medium mt-0.5">
                Passwords do not match.
              </div>
            </div>

            <!-- Compact Password Requirements Indicator -->
            <div class="bg-slate-50 border border-slate-200 rounded-xl p-2.5 text-xs text-[#64748B] space-y-1">
              <span class="font-semibold text-[#1E293B] block mb-0.5">Password requirements</span>
              <div class="flex items-center gap-1.5" [class.text-emerald-600]="hasMinLength()">
                <i class="pi text-[11px]" [class.pi-check-circle]="hasMinLength()" [class.pi-circle]="!hasMinLength()"></i>
                <span>At least 8 characters long</span>
              </div>
              <div class="flex items-center gap-1.5" [class.text-emerald-600]="passwordsMatch()">
                <i class="pi text-[11px]" [class.pi-check-circle]="passwordsMatch()" [class.pi-circle]="!passwordsMatch()"></i>
                <span>Passwords match</span>
              </div>
            </div>

            <!-- Primary Create Account Button -->
            <div class="mt-1 flex flex-col gap-3">
              <p-button
                type="submit"
                [label]="isLoading() ? 'Creating Account...' : 'Create Account'"
                icon="pi pi-arrow-right"
                iconPos="right"
                [loading]="isLoading()"
                [disabled]="registerForm.invalid || isLoading()"
                styleClass="w-full !h-[48px] !rounded-[10px] !bg-[#0F766E] hover:!bg-[#0D665F] !border-[#0F766E] text-white font-semibold text-sm shadow-md shadow-[#0F766E]/20 hover:-translate-y-0.5 transition-all duration-200 cursor-pointer flex items-center justify-center gap-2"
              ></p-button>

              <!-- Sign In Link -->
              <div class="text-center text-sm font-medium text-[#64748B] mt-1">
                Already have an account?
                <a routerLink="/login" class="text-[#0F766E] hover:text-[#0D665F] font-semibold ml-1 hover:underline transition-colors">
                  Sign in
                </a>
              </div>
            </div>

          </form>
        </div>

        <!-- Security / Trust Footer -->
        <div class="text-xs text-[#64748B] font-medium flex items-center justify-between border-t border-slate-200/80 pt-3 max-w-[440px] mx-auto w-full">
          <span class="flex items-center gap-1.5">
            <i class="pi pi-lock text-slate-400 text-xs"></i> Secure encrypted access &bull; Role-based permissions
          </span>
          <span>&copy; {{ currentYear }} Health ERP</span>
        </div>

      </div>

      <!-- RIGHT COLUMN: Healthcare Enterprise Visual Panel (50% on Desktop) -->
      <div class="hidden lg:flex lg:w-1/2 h-full bg-[#071A2B] relative flex-col justify-between p-8 lg:p-12 overflow-hidden text-white">
        
        <!-- Abstract Medical Vector Pattern Overlay -->
        <div class="absolute inset-0 opacity-10 pointer-events-none">
          <svg class="w-full h-full" xmlns="http://www.w3.org/2000/svg">
            <defs>
              <pattern id="register-grid" width="40" height="40" patternUnits="userSpaceOnUse">
                <circle cx="20" cy="20" r="1" fill="#FFFFFF" />
                <path d="M 20 15 L 20 25 M 15 20 L 25 20" stroke="#FFFFFF" stroke-width="0.5" />
              </pattern>
            </defs>
            <rect width="100%" height="100%" fill="url(#register-grid)" />
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
            One platform for your entire hospital.
          </h2>

          <p class="text-sm text-[#94A3B8] font-normal leading-relaxed">
            Manage patients, appointments, clinical workflows, pharmacy, laboratory, beds, emergency care, billing, staff, and analytics from one secure workspace.
          </p>
        </div>

        <!-- Center Decorative Workspace Modules Preview Card -->
        <div class="relative z-10 my-2 lg:my-3 max-w-lg bg-slate-900/85 border border-slate-700/60 rounded-2xl p-4 shadow-2xl backdrop-blur-md transform scale-[0.98] origin-center">
          
          <!-- Mockup Header -->
          <div class="flex items-center justify-between pb-2.5 border-b border-slate-800 mb-3">
            <div class="flex items-center gap-2">
              <div class="w-2.5 h-2.5 rounded-full bg-rose-500/80"></div>
              <div class="w-2.5 h-2.5 rounded-full bg-amber-500/80"></div>
              <div class="w-2.5 h-2.5 rounded-full bg-emerald-500/80"></div>
              <span class="text-xs font-semibold text-slate-300 ml-1.5">Health ERP Workspace</span>
            </div>
            <span class="text-[11px] font-medium bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 px-2 py-0.5 rounded-full flex items-center gap-1">
              <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span> Integrated Modules
            </span>
          </div>

          <!-- 3x3 Feature Grid -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs font-medium text-slate-200">
            <div class="flex items-center gap-2 bg-slate-800/60 border border-slate-700/40 rounded-lg p-2">
              <i class="pi pi-users text-teal-400 text-xs"></i>
              <span>Patient Management</span>
            </div>
            <div class="flex items-center gap-2 bg-slate-800/60 border border-slate-700/40 rounded-lg p-2">
              <i class="pi pi-calendar text-teal-400 text-xs"></i>
              <span>Appointments & Scheduling</span>
            </div>
            <div class="flex items-center gap-2 bg-slate-800/60 border border-slate-700/40 rounded-lg p-2">
              <i class="pi pi-heart text-teal-400 text-xs"></i>
              <span>Pharmacy & Medication</span>
            </div>
            <div class="flex items-center gap-2 bg-slate-800/60 border border-slate-700/40 rounded-lg p-2">
              <i class="pi pi-filter text-teal-400 text-xs"></i>
              <span>Laboratory & Diagnostics</span>
            </div>
            <div class="flex items-center gap-2 bg-slate-800/60 border border-slate-700/40 rounded-lg p-2">
              <i class="pi pi-home text-teal-400 text-xs"></i>
              <span>Inpatient Ward Management</span>
            </div>
            <div class="flex items-center gap-2 bg-slate-800/60 border border-slate-700/40 rounded-lg p-2">
              <i class="pi pi-[#EF4444] pi-bolt text-rose-400 text-xs"></i>
              <span>Emergency & Triage</span>
            </div>
            <div class="flex items-center gap-2 bg-slate-800/60 border border-slate-700/40 rounded-lg p-2">
              <i class="pi pi-dollar text-teal-400 text-xs"></i>
              <span>Billing & Financial Claims</span>
            </div>
            <div class="flex items-center gap-2 bg-slate-800/60 border border-slate-700/40 rounded-lg p-2">
              <i class="pi pi-id-card text-teal-400 text-xs"></i>
              <span>Doctors & Staff</span>
            </div>
          </div>

          <!-- Bottom Full Width Feature item -->
          <div class="mt-2 flex items-center justify-between bg-teal-950/40 border border-teal-500/30 rounded-lg p-2 text-xs font-semibold text-teal-300">
            <div class="flex items-center gap-2">
              <i class="pi pi-chart-bar text-teal-400 text-xs"></i>
              <span>BI Analytics & Revenue Reports</span>
            </div>
            <span class="text-[10px] bg-teal-500/20 text-teal-200 px-1.5 py-0.5 rounded font-mono">Real-time</span>
          </div>

        </div>

        <!-- Bottom 4 Compact Benefit Items -->
        <div class="relative z-10 flex flex-wrap items-center justify-between gap-2 border-t border-slate-800/80 pt-3 max-w-lg text-[11px] font-medium text-slate-300">
          <div class="flex items-center gap-1.5">
            <div class="w-4 h-4 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[9px]">✓</div>
            <span>Secure role-based access</span>
          </div>
          <div class="flex items-center gap-1.5">
            <div class="w-4 h-4 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[9px]">✓</div>
            <span>Centralized hospital operations</span>
          </div>
          <div class="flex items-center gap-1.5">
            <div class="w-4 h-4 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[9px]">✓</div>
            <span>Real-time clinical insights</span>
          </div>
          <div class="flex items-center gap-1.5">
            <div class="w-4 h-4 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[9px]">✓</div>
            <span>Enterprise-ready workflows</span>
          </div>
        </div>

      </div>

    </div>
  `,
})
export class Register {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  currentYear = new Date().getFullYear();

  registerForm = this.fb.group(
    {
      firstName: ['', [Validators.required]],
      lastName: ['', [Validators.required]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordMatchValidator }
  );

  hasMinLength(): boolean {
    const val = this.registerForm.get('password')?.value;
    return !!val && val.length >= 8;
  }

  passwordsMatch(): boolean {
    const pass = this.registerForm.get('password')?.value;
    const confirm = this.registerForm.get('confirmPassword')?.value;
    return !!pass && pass === confirm;
  }

  onSubmit(): void {
    if (this.registerForm.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { firstName, lastName, email, password } = this.registerForm.value;

    this.authService
      .register({
        firstName: firstName!.trim(),
        lastName: lastName!.trim(),
        email: email!.trim(),
        password: password!,
      })
      .subscribe({
        next: () => {
          this.isLoading.set(false);
          Swal.fire({
            title: 'Account Created!',
            text: `Welcome ${firstName}! Your account was created successfully.`,
            icon: 'success',
            confirmButtonColor: '#0f766e',
            timer: 2500,
            timerProgressBar: true,
          });
          this.router.navigate(['/portal-coming-soon']);
        },
        error: (err) => {
          this.isLoading.set(false);
          this.errorMessage.set(err.error?.message || (err.error?.errors ? err.error.errors.join(', ') : 'We couldn\'t create your account. Please try again.'));
        },
      });
  }
}
