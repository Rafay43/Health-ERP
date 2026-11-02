import { Component, inject, signal, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './landing.component.html',
})
export class LandingComponent implements OnInit {
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);
  private router = inject(Router);

  // Global Math & Date for template
  Math = Math;
  currentYear = new Date().getFullYear();

  // State Signals
  isScrolled = signal<boolean>(false);
  mobileMenuOpen = signal<boolean>(false);
  submittingContact = signal<boolean>(false);
  contactSubmitted = signal<boolean>(false);
  contactSuccess = signal<boolean>(false);
  contactError = signal<string | null>(null);

  // Authentication Status
  isAuthenticated = computed(() => this.authService.isAuthenticated());

  // Contact Form
  contactForm = this.fb.group({
    fullName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    organization: [''],
    requestType: ['Product Demo', [Validators.required]],
    subject: ['', [Validators.required]],
    message: ['', [Validators.required, Validators.minLength(10)]],
  });

  ngOnInit(): void {
    // Check initial scroll state
    this.checkScroll();
  }

  @HostListener('window:scroll', [])
  onWindowScroll(): void {
    this.checkScroll();
  }

  private checkScroll(): void {
    if (typeof window !== 'undefined') {
      this.isScrolled.set(window.scrollY > 20);
    }
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.set(!this.mobileMenuOpen());
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  scrollToSection(sectionId: string, event?: Event): void {
    if (event) {
      event.preventDefault();
    }
    this.closeMobileMenu();
    const el = document.getElementById(sectionId);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  navigateToLogin(): void {
    if (this.isAuthenticated()) {
      this.router.navigate(['/dashboard']);
    } else {
      this.router.navigate(['/login']);
    }
  }

  submitContactForm(): void {
    this.contactSubmitted.set(true);

    if (this.contactForm.invalid) {
      this.contactForm.markAllAsTouched();
      return;
    }

    this.submittingContact.set(true);
    this.contactError.set(null);

    // TODO: Connect this form submission to backend contact/support API endpoint when available (/api/v1/contact)
    const val = this.contactForm.value;

    // Simulate clean network dispatch
    setTimeout(() => {
      this.submittingContact.set(false);
      this.contactSuccess.set(true);

      Swal.fire({
        title: 'Message Sent Successfully',
        text: `Thank you ${val.fullName}! Our Health ERP product support team will review your inquiry and get back to you shortly.`,
        icon: 'success',
        confirmButtonColor: '#0F766E',
        timer: 3500,
        timerProgressBar: true,
      });

      this.contactForm.reset({
        fullName: '',
        email: '',
        organization: '',
        requestType: 'Product Demo',
        subject: '',
        message: '',
      });
      this.contactSubmitted.set(false);
    }, 900);
  }
}

// Helper Computed function inside file scope
function computed<T>(fn: () => T) {
  return signal(fn());
}
