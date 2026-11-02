import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

import { AuthService } from '../../../core/services/auth.service';
import Swal from 'sweetalert2';

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('newPassword');
  const confirmPassword = control.get('confirmPassword');
  if (password && confirmPassword && password.value !== confirmPassword.value) {
    return { passwordMismatch: true };
  }
  return null;
}

@Component({
  selector: 'app-reset-password',
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
      
      <!-- LEFT COLUMN: Reset Password Form Section (50% on Desktop) -->
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

        <!-- Main Container -->
        <div class="w-full max-w-[420px] mx-auto my-auto pt-4 pb-2 lg:pt-6 lg:pb-3">
          
          <!-- STATE 1: MISSING OR INVALID QUERY PARAMS -->
          <ng-container *ngIf="isInvalidLink()">
            <div class="text-center py-4">
              <div class="w-16 h-16 rounded-2xl bg-rose-50 border border-rose-200 text-[#DC2626] flex items-center justify-center mx-auto mb-5 shadow-sm">
                <i class="pi pi-exclamation-triangle text-2xl"></i>
              </div>

              <h1 class="text-2xl font-bold text-[#0F172A] mb-3 tracking-tight">Invalid or Expired Link</h1>
              <p class="text-sm text-[#64748B] leading-relaxed mb-6">
                This password reset link is invalid, incomplete, or has expired. Please request a new password reset link.
              </p>

              <a routerLink="/forgot-password" class="w-full h-11 rounded-[10px] bg-[#0F766E] hover:bg-[#0D665F] text-white font-semibold text-sm transition-colors cursor-pointer flex items-center justify-center gap-2 shadow-sm">
                <i class="pi pi-key text-xs"></i> Request New Reset Link
              </a>
            </div>
          </ng-container>

          <!-- STATE 2: SUCCESS STATE -->
          <ng-container *ngIf="resetSuccess() && !isInvalidLink()">
            <div class="text-center py-4">
              <div class="w-16 h-16 rounded-2xl bg-teal-50 border border-teal-200 text-[#0F766E] flex items-center justify-center mx-auto mb-5 shadow-sm">
                <i class="pi pi-check-circle text-3xl"></i>
              </div>

              <h1 class="text-2xl font-bold text-[#0F172A] mb-3 tracking-tight">Password Reset Complete</h1>
              <p class="text-sm text-[#64748B] leading-relaxed mb-6">
                Your password has been successfully updated. Active sessions have been invalidated for security. You can now sign in with your new password.
              </p>

              <a routerLink="/login" class="w-full h-11 rounded-[10px] bg-[#0F766E] hover:bg-[#0D665F] text-white font-semibold text-sm transition-colors cursor-pointer flex items-center justify-center gap-2 shadow-sm">
                <i class="pi pi-sign-in text-xs"></i> Sign In to Health ERP
              </a>
            </div>
          </ng-container>

          <!-- STATE 3: RESET PASSWORD FORM -->
          <ng-container *ngIf="!isInvalidLink() && !resetSuccess()">
            <div class="mb-6">
              <h1 class="text-3xl lg:text-[32px] font-bold text-[#0F172A] tracking-tight mb-2 leading-tight">Set new password</h1>
              <p class="text-sm font-normal text-[#64748B] leading-relaxed">
                Create a strong new password for <strong class="text-[#0F172A]">{{ targetEmail() }}</strong>.
              </p>
            </div>

            <form [formGroup]="resetForm" (ngSubmit)="onSubmit()" class="flex flex-col gap-5">
              
              <!-- Error Alert -->
              <div *ngIf="errorMessage()" class="mb-1">
                <p-message severity="error">{{ errorMessage()! }}</p-message>
              </div>

              <!-- New Password Field -->
              <div class="flex flex-col gap-1.5">
                <label for="newPassword" class="text-sm font-semibold text-[#1E293B]">New Password</label>
                <div class="relative flex items-center">
                  <i class="pi pi-lock absolute left-4 text-slate-400 text-base z-10 pointer-events-none"></i>
                  <p-password
                    id="newPassword"
                    formControlName="newPassword"
                    [feedback]="false"
                    [toggleMask]="true"
                    styleClass="w-full"
                    inputStyleClass="w-full !h-[50px] !pl-11 !py-3 !pr-10 text-sm font-medium rounded-[10px] border border-slate-300 focus:border-[#0F766E] focus:ring-2 focus:ring-[#0F766E]/20 transition-all duration-200 text-[#0F172A] bg-white"
                    placeholder="At least 8 characters"
                  ></p-password>
                </div>
                <div *ngIf="resetForm.get('newPassword')?.touched && resetForm.get('newPassword')?.hasError('required')" class="text-xs text-[#DC2626] font-medium mt-1">
                  New password is required.
                </div>
                <div *ngIf="resetForm.get('newPassword')?.touched && resetForm.get('newPassword')?.hasError('minlength')" class="text-xs text-[#DC2626] font-medium mt-1">
                  Password must be at least 8 characters long.
                </div>
              </div>

              <!-- Confirm Password Field -->
              <div class="flex flex-col gap-1.5">
                <label for="confirmPassword" class="text-sm font-semibold text-[#1E293B]">Confirm New Password</label>
                <div class="relative flex items-center">
                  <i class="pi pi-shield absolute left-4 text-slate-400 text-base z-10 pointer-events-none"></i>
                  <p-password
                    id="confirmPassword"
                    formControlName="confirmPassword"
                    [feedback]="false"
                    [toggleMask]="true"
                    styleClass="w-full"
                    inputStyleClass="w-full !h-[50px] !pl-11 !py-3 !pr-10 text-sm font-medium rounded-[10px] border border-slate-300 focus:border-[#0F766E] focus:ring-2 focus:ring-[#0F766E]/20 transition-all duration-200 text-[#0F172A] bg-white"
                    placeholder="Re-enter new password"
                  ></p-password>
                </div>
                <div *ngIf="resetForm.touched && resetForm.hasError('passwordMismatch')" class="text-xs text-[#DC2626] font-medium mt-1">
                  Passwords do not match.
                </div>
              </div>

              <!-- Password Requirements Card -->
              <div class="bg-slate-50 border border-slate-200 rounded-xl p-3 text-xs text-[#64748B] space-y-1.5">
                <div class="font-semibold text-[#1E293B] mb-1">Password criteria:</div>
                <div class="flex items-center gap-2" [class.text-emerald-600]="hasMinLength()">
                  <i class="pi text-xs" [class.pi-check-circle]="hasMinLength()" [class.pi-circle]="!hasMinLength()"></i>
                  <span>At least 8 characters long</span>
                </div>
                <div class="flex items-center gap-2" [class.text-emerald-600]="passwordsMatch()">
                  <i class="pi text-xs" [class.pi-check-circle]="passwordsMatch()" [class.pi-circle]="!passwordsMatch()"></i>
                  <span>Passwords match</span>
                </div>
              </div>

              <!-- Primary Submit Button -->
              <div class="mt-1 flex flex-col gap-4">
                <p-button
                  type="submit"
                  label="Update Password"
                  icon="pi pi-check"
                  iconPos="right"
                  [loading]="isLoading()"
                  [disabled]="resetForm.invalid || isLoading()"
                  styleClass="w-full !h-[50px] !rounded-[10px] !bg-[#0F766E] hover:!bg-[#0D665F] !border-[#0F766E] text-white font-semibold text-sm shadow-md shadow-[#0F766E]/20 hover:-translate-y-0.5 transition-all duration-200 cursor-pointer flex items-center justify-center gap-2"
                ></p-button>

                <!-- Return to Sign In Link -->
                <div class="text-center text-sm font-medium text-[#64748B] mt-2">
                  <a routerLink="/login" class="text-[#0F766E] hover:text-[#0D665F] font-semibold inline-flex items-center gap-2 hover:underline transition-colors">
                    <i class="pi pi-arrow-left text-xs"></i> Cancel and back to sign in
                  </a>
                </div>
              </div>

            </form>
          </ng-container>

        </div>

        <!-- Security / Trust Footer -->
        <div class="text-xs text-[#64748B] font-medium flex items-center justify-between border-t border-slate-200/80 pt-4 max-w-[420px] mx-auto w-full">
          <span class="flex items-center gap-1.5">
            <i class="pi pi-lock text-slate-400 text-xs"></i> Encrypted Session &bull; Security Stamp Update
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
              <pattern id="reset-grid" width="40" height="40" patternUnits="userSpaceOnUse">
                <circle cx="20" cy="20" r="1" fill="#FFFFFF" />
                <path d="M 20 15 L 20 25 M 15 20 L 25 20" stroke="#FFFFFF" stroke-width="0.5" />
              </pattern>
            </defs>
            <rect width="100%" height="100%" fill="url(#reset-grid)" />
            <path d="M 0 300 Q 150 240, 300 300 T 600 300 T 900 300" fill="none" stroke="#0F766E" stroke-width="2" opacity="0.3" />
          </svg>
        </div>

        <!-- Subtle Ambient Glows -->
        <div class="absolute top-0 right-0 w-96 h-96 bg-[#0F766E]/20 rounded-full blur-[100px] pointer-events-none"></div>
        <div class="absolute bottom-0 left-0 w-96 h-96 bg-[#38BDF8]/10 rounded-full blur-[100px] pointer-events-none"></div>

        <!-- Upper Content Section -->
        <div class="relative z-10 max-w-lg">
          <div class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-[#0F766E]/20 text-[#CCFBF1] border border-[#0F766E]/30 mb-3">
            <i class="pi pi-lock text-xs"></i> Password Security Confirmation
          </div>

          <h2 class="text-2xl lg:text-3xl font-bold tracking-tight text-white mb-2 leading-snug">
            Protected Hospital Data & Account Integrity
          </h2>

          <p class="text-sm text-[#94A3B8] font-normal leading-relaxed">
            Updating your password instantly revokes all existing refresh tokens and active web sessions across all devices.
          </p>
        </div>

        <!-- Security Card -->
        <div class="relative z-10 my-3 lg:my-3.5 max-w-lg bg-slate-900/85 border border-slate-700/60 rounded-2xl p-5 shadow-2xl backdrop-blur-md">
          <div class="flex items-center gap-3.5 mb-4">
            <div class="w-10 h-10 rounded-xl bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center">
              <i class="pi pi-check-square text-lg"></i>
            </div>
            <div>
              <h3 class="text-sm font-bold text-white leading-tight">Security Posture Verification</h3>
              <p class="text-xs text-slate-400">Automatic Credential Verification</p>
            </div>
          </div>

          <div class="space-y-2.5 text-xs text-slate-300">
            <div class="flex items-center gap-2">
              <i class="pi pi-check text-emerald-400 text-xs"></i>
              <span>Tokens single-use validated via Identity Core</span>
            </div>
            <div class="flex items-center gap-2">
              <i class="pi pi-check text-emerald-400 text-xs"></i>
              <span>Security Stamp automatically updated</span>
            </div>
            <div class="flex items-center gap-2">
              <i class="pi pi-check text-emerald-400 text-xs"></i>
              <span>Role-based permissions preserved</span>
            </div>
          </div>
        </div>

        <!-- Bottom Benefits -->
        <div class="relative z-10 flex items-center justify-between gap-3 border-t border-slate-800/80 pt-3.5 max-w-lg text-xs font-medium text-slate-300">
          <div class="flex items-center gap-2">
            <div class="w-4.5 h-4.5 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[10px]">✓</div>
            <span>Immediate Protection</span>
          </div>
          <div class="flex items-center gap-2">
            <div class="w-4.5 h-4.5 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[10px]">✓</div>
            <span>Enterprise Compliance</span>
          </div>
        </div>

      </div>

    </div>
  `,
})
export class ResetPasswordComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private authService = inject(AuthService);

  isLoading = signal(false);
  isInvalidLink = signal(false);
  resetSuccess = signal(false);
  targetEmail = signal('');
  targetToken = signal('');
  errorMessage = signal<string | null>(null);
  currentYear = new Date().getFullYear();

  resetForm = this.fb.group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordMatchValidator }
  );

  ngOnInit(): void {
    const email = this.route.snapshot.queryParamMap.get('email');
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!email || !token) {
      this.isInvalidLink.set(true);
    } else {
      this.targetEmail.set(email);
      this.targetToken.set(token);
    }
  }

  hasMinLength(): boolean {
    const val = this.resetForm.get('newPassword')?.value;
    return !!val && val.length >= 8;
  }

  passwordsMatch(): boolean {
    const pass = this.resetForm.get('newPassword')?.value;
    const confirm = this.resetForm.get('confirmPassword')?.value;
    return !!pass && pass === confirm;
  }

  onSubmit(): void {
    if (this.resetForm.invalid || this.isInvalidLink()) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const request = {
      email: this.targetEmail(),
      token: this.targetToken(),
      newPassword: this.resetForm.value.newPassword!,
    };

    this.authService.resetPassword(request).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        if (res.success) {
          this.resetSuccess.set(true);
          Swal.fire({
            title: 'Password Reset!',
            text: 'Your password has been reset successfully.',
            icon: 'success',
            confirmButtonColor: '#0f766e',
            timer: 2500,
            timerProgressBar: true
          });
        } else {
          this.errorMessage.set(res.message || (res.errors ? res.errors.join(', ') : 'Password reset failed.'));
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        const backendError = err.error?.errors ? err.error.errors.join(', ') : (err.error?.message || 'Password reset failed or token expired.');
        this.errorMessage.set(backendError);
      },
    });
  }
}
