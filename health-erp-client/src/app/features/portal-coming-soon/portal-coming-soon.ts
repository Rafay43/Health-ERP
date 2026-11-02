import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import {
  PatientAccountService,
  LinkedPatientSummary,
} from '../../core/services/patient-account.service';

@Component({
  selector: 'app-portal-coming-soon',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './portal-coming-soon.html',
})
export class PortalComingSoonComponent implements OnInit, OnDestroy {
  private authService = inject(AuthService);
  private patientAccountService = inject(PatientAccountService);
  private router = inject(Router);
  private fb = inject(FormBuilder);

  readonly currentUser = this.authService.currentUser;

  // View States: 'loading' | 'unlinked' | 'verify' | 'linked'
  viewState = signal<'loading' | 'unlinked' | 'verify' | 'linked'>('loading');

  // Status & Feedback
  isLinked = signal(false);
  linkedPatient = signal<LinkedPatientSummary | null>(null);
  maskedDestination = signal<string | null>(null);

  errorMessage = signal<string | null>(null);
  infoMessage = signal<string | null>(null);
  requiresSupport = signal(false);
  submitting = signal(false);

  // Resend Cooldown Timer
  cooldownSeconds = signal(0);
  private timerInterval: any = null;

  // Forms
  startForm = this.fb.group({
    medicalRecordNumber: ['', [Validators.required]],
    dateOfBirth: ['', [Validators.required]],
  });

  verifyForm = this.fb.group({
    code: ['', [Validators.required, Validators.pattern('^[0-9]{6}$')]],
  });

  ngOnInit(): void {
    this.checkStatus();
  }

  checkStatus(): void {
    this.viewState.set('loading');
    this.patientAccountService.getStatus().subscribe({
      next: (status) => {
        this.isLinked.set(status.isLinked);
        if (status.isLinked) {
          this.router.navigate(['/portal']);
        } else {
          this.viewState.set('unlinked');
        }
      },
      error: () => {
        this.viewState.set('unlinked');
      },
    });
  }

  onStartSubmit(): void {
    if (this.startForm.invalid || this.submitting() || this.cooldownSeconds() > 0) return;

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.infoMessage.set(null);
    this.requiresSupport.set(false);

    const mrn = this.startForm.value.medicalRecordNumber!.trim();
    const dob = this.startForm.value.dateOfBirth!;

    this.patientAccountService.startActivation(mrn, dob).subscribe({
      next: (res) => {
        this.submitting.set(false);
        this.startCooldown(res.resendCooldownUntilUtc);
        if (res.requiresSupport) {
          this.requiresSupport.set(true);
          this.errorMessage.set(res.message);
          return;
        }

        if (res.success) {
          this.maskedDestination.set(res.maskedDestination || 'contact email on file');
          this.infoMessage.set(res.message);
          this.viewState.set('verify');
        } else {
          this.errorMessage.set(res.message);
        }
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(
          err.error?.detail || err.error?.message || 'Unable to initiate portal activation. Please verify your details.'
        );
      },
    });
  }

  onVerifySubmit(): void {
    if (this.verifyForm.invalid || this.submitting()) return;

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.infoMessage.set(null);

    const code = this.verifyForm.value.code!.trim();

    this.patientAccountService.verifyActivation(code).subscribe({
      next: (res) => {
        this.submitting.set(false);
        if (res.success) {
          this.isLinked.set(true);
          this.router.navigate(['/portal']);
        } else {
          this.errorMessage.set(res.message);
        }
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.detail || err.error?.message || 'Verification failed. Please check the code.');
      },
    });
  }

  resendCode(): void {
    if (this.cooldownSeconds() > 0 || this.submitting()) return;
    this.onStartSubmit();
  }

  backToStart(): void {
    if (this.submitting()) return;
    this.errorMessage.set(null);
    this.infoMessage.set(null);
    this.requiresSupport.set(false);
    this.viewState.set('unlinked');
  }

  logout(): void {
    if (this.timerInterval) clearInterval(this.timerInterval);
    this.authService.logout();
  }

  ngOnDestroy(): void {
    if (this.timerInterval) clearInterval(this.timerInterval);
  }

  private startCooldown(deadline?: string | null): void {
    if (!deadline) return;
    const until = Date.parse(deadline);
    if (!Number.isFinite(until)) return;
    const update = () => this.cooldownSeconds.set(Math.max(0, Math.ceil((until - Date.now()) / 1000)));
    update();
    if (this.timerInterval) clearInterval(this.timerInterval);

    this.timerInterval = setInterval(() => {
      update();
      if (this.cooldownSeconds() === 0) {
        clearInterval(this.timerInterval);
      }
    }, 1000);
  }
}
