import { TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import Swal from 'sweetalert2';
import { AuthService } from '../../core/services/auth.service';
import { LandingComponent } from './landing.component';

vi.mock('sweetalert2', () => ({ default: { fire: vi.fn(() => Promise.resolve({})) } }));

describe('Landing contact privacy', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('preserves submission feedback and reset without logging personal data', () => {
    vi.useFakeTimers();
    const log = vi.spyOn(console, 'log').mockImplementation(() => {});
    TestBed.configureTestingModule({ providers: [
      FormBuilder,
      { provide: AuthService, useValue: { isAuthenticated: () => false } },
      { provide: Router, useValue: { navigate: vi.fn() } },
    ] });
    const component = TestBed.runInInjectionContext(() => new LandingComponent());
    component.contactForm.setValue({
      fullName: 'Demo Contact', email: 'demo.contact@example.com', organization: 'Fictional Demo Clinic',
      requestType: 'Product Demo', subject: 'Synthetic inquiry', message: 'Fictional message for a privacy regression test.',
    });
    component.submitContactForm();
    expect(component.submittingContact()).toBe(true);
    expect(log).not.toHaveBeenCalled();
    vi.advanceTimersByTime(900);
    expect(component.submittingContact()).toBe(false);
    expect(component.contactSuccess()).toBe(true);
    expect(Swal.fire).toHaveBeenCalledWith(expect.objectContaining({ icon: 'success' }));
    expect(component.contactForm.value.fullName).toBe('');
    expect(component.contactForm.value.email).toBe('');
    expect(component.contactForm.value.message).toBe('');
    expect(log).not.toHaveBeenCalled();
  });
});
