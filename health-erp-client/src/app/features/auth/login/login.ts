import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    CardModule,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    MessageModule,
  ],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-slate-900 p-4">
      <div class="w-full max-w-md">
        <p-card header="Health ERP - Sign In" styleClass="p-shadow-lg">
          <form [formGroup]="loginForm" (ngSubmit)="onSubmit()" class="flex flex-col gap-4 mt-2">
            
            <div *ngIf="errorMessage()" class="mb-2">
              <p-message severity="error" [text]="errorMessage()!"></p-message>
            </div>

            <div class="flex flex-col gap-1">
              <label for="email" class="text-sm font-medium text-slate-300">Email Address</label>
              <input
                id="email"
                type="email"
                pInputText
                formControlName="email"
                placeholder="admin@healtherp.com"
                class="w-full"
              />
            </div>

            <div class="flex flex-col gap-1">
              <label for="password" class="text-sm font-medium text-slate-300">Password</label>
              <p-password
                id="password"
                formControlName="password"
                [feedback]="false"
                [toggleMask]="true"
                styleClass="w-full"
                inputStyleClass="w-full"
                placeholder="••••••••"
              ></p-password>
            </div>

            <div class="mt-4 flex flex-col gap-2">
              <p-button
                type="submit"
                label="Sign In"
                icon="pi pi-sign-in"
                [loading]="isLoading()"
                [disabled]="loginForm.invalid || isLoading()"
                styleClass="w-full p-button-primary"
              ></p-button>

              <div class="text-center mt-2 text-sm text-slate-400">
                Don't have an account?
                <a routerLink="/register" class="text-indigo-400 hover:underline font-semibold ml-1">
                  Create Account
                </a>
              </div>
            </div>
          </form>
        </p-card>
      </div>
    </div>
  `,
})
export class Login {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  loginForm = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { email, password } = this.loginForm.value;

    this.authService.login({ email: email!, password: password! }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Invalid email or password.');
      },
    });
  }
}
