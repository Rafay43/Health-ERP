import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    InputTextModule,
    ButtonModule,
    MessageModule,
  ],
  template: `
    <div class="min-h-screen lg:h-screen lg:max-h-screen w-full flex bg-[#F4F7FA] font-sans antialiased text-[#1E293B] lg:overflow-hidden">
      
      <!-- LEFT COLUMN: Forgot Password Form Section (50% on Desktop) -->
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

        <!-- Main Form Container -->
        <div class="w-full max-w-[420px] mx-auto my-auto pt-4 pb-2 lg:pt-6 lg:pb-3">
          
          <!-- STATE 1: INITIAL INPUT FORM -->
          <ng-container *ngIf="!submitted()">
            <div class="mb-6">
              <h1 class="text-3xl lg:text-[32px] font-bold text-[#0F172A] tracking-tight mb-2 leading-tight">Forgot password?</h1>
              <p class="text-sm font-normal text-[#64748B] leading-relaxed">
                Enter your registered hospital email address below. We'll send you secure instructions to reset your password.
              </p>
            </div>

            <form [formGroup]="forgotForm" (ngSubmit)="onSubmit()" class="flex flex-col gap-5">
              
              <!-- Error Alert -->
              <div *ngIf="errorMessage()" class="mb-1">
                <p-message severity="error" [text]="errorMessage()!"></p-message>
              </div>

              <!-- Email Field -->
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
                <div *ngIf="forgotForm.get('email')?.touched && forgotForm.get('email')?.invalid" class="text-xs text-[#DC2626] font-medium mt-1">
                  Please enter a valid email address.
                </div>
              </div>

              <!-- Primary Submit Button -->
              <div class="mt-2 flex flex-col gap-4">
                <button
                  type="submit"
                  pButton
                  label="Send Reset Link"
                  icon="pi pi-send"
                  iconPos="right"
                  [loading]="isLoading()"
                  [disabled]="forgotForm.invalid || isLoading()"
                  class="w-full !h-[50px] !rounded-[10px] !bg-[#0F766E] hover:!bg-[#0D665F] !border-[#0F766E] text-white font-semibold text-sm shadow-md shadow-[#0F766E]/20 hover:-translate-y-0.5 transition-all duration-200 cursor-pointer flex items-center justify-center gap-2"
                ></button>

                <!-- Return to Sign In Link -->
                <div class="text-center text-sm font-medium text-[#64748B] mt-2">
                  <a routerLink="/login" class="text-[#0F766E] hover:text-[#0D665F] font-semibold inline-flex items-center gap-2 hover:underline transition-colors">
                    <i class="pi pi-arrow-left text-xs"></i> Back to sign in
                  </a>
                </div>
              </div>

            </form>
          </ng-container>

          <!-- STATE 2: CONFIRMATION MESSAGE (Account Enumeration Protected) -->
          <ng-container *ngIf="submitted()">
            <div class="text-center py-4">
              <div class="w-16 h-16 rounded-2xl bg-teal-50 border border-teal-200 text-[#0F766E] flex items-center justify-center mx-auto mb-5 shadow-sm">
                <i class="pi pi-envelope text-2xl"></i>
              </div>

              <h2 class="text-2xl font-bold text-[#0F172A] mb-3 tracking-tight">Check your email</h2>

              <p class="text-sm font-normal text-[#64748B] leading-relaxed mb-6">
                If an account exists for <strong class="text-[#0F172A] font-semibold">{{ submittedEmail() }}</strong>, password reset instructions have been sent. Please check your inbox and spam folder.
              </p>

              <div class="bg-slate-50 border border-slate-200 rounded-xl p-4 text-xs text-[#64748B] text-left mb-6 space-y-2">
                <div class="flex items-start gap-2">
                  <i class="pi pi-info-circle text-[#0F766E] text-sm mt-0.5"></i>
                  <span>Reset links remain valid for <strong>30 minutes</strong> for security reasons.</span>
                </div>
                <div class="flex items-start gap-2">
                  <i class="pi pi-shield text-slate-400 text-sm mt-0.5"></i>
                  <span>If you don't receive an email within a few minutes, verify the address or contact hospital IT support.</span>
                </div>
              </div>

              <div class="flex flex-col gap-3">
                <button
                  type="button"
                  (click)="resetForm()"
                  class="w-full h-11 rounded-[10px] bg-slate-100 hover:bg-slate-200 text-[#0F172A] font-semibold text-sm transition-colors cursor-pointer flex items-center justify-center gap-2"
                >
                  <i class="pi pi-[#0F766E] pi-refresh text-xs"></i> Send to another email
                </button>

                <a routerLink="/login" class="w-full h-11 rounded-[10px] bg-[#0F766E] hover:bg-[#0D665F] text-white font-semibold text-sm transition-colors cursor-pointer flex items-center justify-center gap-2 shadow-sm">
                  <i class="pi pi-arrow-left text-xs"></i> Return to sign in
                </a>
              </div>
            </div>
          </ng-container>

        </div>

        <!-- Security / Trust Footer -->
        <div class="text-xs text-[#64748B] font-medium flex items-center justify-between border-t border-slate-200/80 pt-4 max-w-[420px] mx-auto w-full">
          <span class="flex items-center gap-1.5">
            <i class="pi pi-lock text-slate-400 text-xs"></i> 256-bit SSL Encrypted &bull; Identity Protection
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
              <pattern id="forgot-grid" width="40" height="40" patternUnits="userSpaceOnUse">
                <circle cx="20" cy="20" r="1" fill="#FFFFFF" />
                <path d="M 20 15 L 20 25 M 15 20 L 25 20" stroke="#FFFFFF" stroke-width="0.5" />
              </pattern>
            </defs>
            <rect width="100%" height="100%" fill="url(#forgot-grid)" />
            <path d="M 0 300 Q 150 240, 300 300 T 600 300 T 900 300" fill="none" stroke="#0F766E" stroke-width="2" opacity="0.3" />
          </svg>
        </div>

        <!-- Subtle Ambient Glows -->
        <div class="absolute top-0 right-0 w-96 h-96 bg-[#0F766E]/20 rounded-full blur-[100px] pointer-events-none"></div>
        <div class="absolute bottom-0 left-0 w-96 h-96 bg-[#38BDF8]/10 rounded-full blur-[100px] pointer-events-none"></div>

        <!-- Upper Content Section -->
        <div class="relative z-10 max-w-lg">
          <div class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-[#0F766E]/20 text-[#CCFBF1] border border-[#0F766E]/30 mb-3">
            <i class="pi pi-key text-xs"></i> Account Recovery Protocol
          </div>

          <h2 class="text-2xl lg:text-3xl font-bold tracking-tight text-white mb-2 leading-snug">
            Enterprise Security & Zero-Trust Password Recovery
          </h2>

          <p class="text-sm text-[#94A3B8] font-normal leading-relaxed">
            Health ERP implements strict Identity DataProtection tokens to safeguard clinical accounts and prevent unauthorized credential resets.
          </p>
        </div>

        <!-- Center Security Card -->
        <div class="relative z-10 my-3 lg:my-3.5 max-w-lg bg-slate-900/85 border border-slate-700/60 rounded-2xl p-5 shadow-2xl backdrop-blur-md">
          <div class="flex items-center gap-3.5 mb-4">
            <div class="w-10 h-10 rounded-xl bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center">
              <i class="pi pi-shield text-lg"></i>
            </div>
            <div>
              <h3 class="text-sm font-bold text-white leading-tight">Identity Token Security</h3>
              <p class="text-xs text-slate-400">ASP.NET Core Identity &bull; Cryptographic Tokens</p>
            </div>
          </div>

          <div class="space-y-3 text-xs text-slate-300">
            <div class="flex items-center justify-between p-2.5 rounded-lg bg-slate-800/60 border border-slate-700/40">
              <span class="text-slate-400">Token Expiration</span>
              <span class="font-semibold text-emerald-400">30 Minutes</span>
            </div>
            <div class="flex items-center justify-between p-2.5 rounded-lg bg-slate-800/60 border border-slate-700/40">
              <span class="text-slate-400">Single-Use Protection</span>
              <span class="font-semibold text-sky-400">Enabled</span>
            </div>
            <div class="flex items-center justify-between p-2.5 rounded-lg bg-slate-800/60 border border-slate-700/40">
              <span class="text-slate-400">Session Stamp Invalidation</span>
              <span class="font-semibold text-teal-300">Automatic</span>
            </div>
          </div>
        </div>

        <!-- Bottom Benefits -->
        <div class="relative z-10 flex items-center justify-between gap-3 border-t border-slate-800/80 pt-3.5 max-w-lg text-xs font-medium text-slate-300">
          <div class="flex items-center gap-2">
            <div class="w-4.5 h-4.5 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[10px]">✓</div>
            <span>No Account Enumeration</span>
          </div>
          <div class="flex items-center gap-2">
            <div class="w-4.5 h-4.5 rounded-full bg-[#0F766E]/30 text-[#CCFBF1] flex items-center justify-center text-[10px]">✓</div>
            <span>Encrypted Email Link</span>
          </div>
        </div>

      </div>

    </div>
  `,
})
export class ForgotPasswordComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);

  isLoading = signal(false);
  submitted = signal(false);
  submittedEmail = signal('');
  errorMessage = signal<string | null>(null);
  currentYear = new Date().getFullYear();

  forgotForm = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
  });

  onSubmit(): void {
    if (this.forgotForm.invalid) return;

    const email = this.forgotForm.value.email!.trim();
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService.forgotPassword(email).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        this.submittedEmail.set(email);
        this.submitted.set(true);
      },
      error: (err) => {
        this.isLoading.set(false);
        // Even on error, show generic confirmation unless network completely failed
        this.submittedEmail.set(email);
        this.submitted.set(true);
      },
    });
  }

  resetForm(): void {
    this.submitted.set(false);
    this.forgotForm.reset();
  }
}
