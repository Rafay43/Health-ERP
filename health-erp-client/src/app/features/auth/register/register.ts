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
  selector: 'app-register',
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
        <p-card header="Health ERP - Register Account" styleClass="p-shadow-lg">
          <form [formGroup]="registerForm" (ngSubmit)="onSubmit()" class="flex flex-col gap-4 mt-2">
            
            <div *ngIf="errorMessage()" class="mb-2">
              <p-message severity="error" [text]="errorMessage()!"></p-message>
            </div>

            <div class="grid grid-cols-2 gap-2">
              <div class="flex flex-col gap-1">
                <label for="firstName" class="text-sm font-medium text-slate-300">First Name</label>
                <input id="firstName" pInputText formControlName="firstName" placeholder="John" />
              </div>
              <div class="flex flex-col gap-1">
                <label for="lastName" class="text-sm font-medium text-slate-300">Last Name</label>
                <input id="lastName" pInputText formControlName="lastName" placeholder="Doe" />
              </div>
            </div>

            <div class="flex flex-col gap-1">
              <label for="email" class="text-sm font-medium text-slate-300">Email Address</label>
              <input
                id="email"
                type="email"
                pInputText
                formControlName="email"
                placeholder="john.doe@healtherp.com"
                class="w-full"
              />
            </div>

            <div class="flex flex-col gap-1">
              <label for="password" class="text-sm font-medium text-slate-300">Password</label>
              <p-password
                id="password"
                formControlName="password"
                [toggleMask]="true"
                styleClass="w-full"
                inputStyleClass="w-full"
                placeholder="Minimum 8 characters"
              ></p-password>
            </div>

            <div class="mt-4 flex flex-col gap-2">
              <p-button
                type="submit"
                label="Create Account"
                icon="pi pi-user-plus"
                [loading]="isLoading()"
                [disabled]="registerForm.invalid || isLoading()"
                styleClass="w-full p-button-success"
              ></p-button>

              <div class="text-center mt-2 text-sm text-slate-400">
                Already have an account?
                <a routerLink="/login" class="text-indigo-400 hover:underline font-semibold ml-1">
                  Sign In
                </a>
              </div>
            </div>
          </form>
        </p-card>
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

  registerForm = this.fb.group({
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  onSubmit(): void {
    if (this.registerForm.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { firstName, lastName, email, password } = this.registerForm.value;

    this.authService
      .register({
        firstName: firstName!,
        lastName: lastName!,
        email: email!,
        password: password!,
      })
      .subscribe({
        next: () => {
          this.isLoading.set(false);
          this.router.navigate(['/dashboard']);
        },
        error: (err) => {
          this.isLoading.set(false);
          this.errorMessage.set(err.error?.message || 'Registration failed. Please try again.');
        },
      });
  }
}
