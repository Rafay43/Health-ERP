import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { PortalComingSoonComponent } from './portal-coming-soon';
import { PatientAccountService } from '../../core/services/patient-account.service';
import { AuthService } from '../../core/services/auth.service';

describe('Activation integrity UI', () => {
  let component: PortalComingSoonComponent;
  let start: Subject<any>;
  let verify: Subject<any>;
  let service: any;
  beforeEach(() => {
    vi.useFakeTimers(); vi.setSystemTime(new Date('2026-10-13T00:00:00Z'));
    start = new Subject(); verify = new Subject();
    service = { startActivation: vi.fn(() => start), verifyActivation: vi.fn(() => verify), getStatus: vi.fn(() => of({ isLinked: false })) };
    TestBed.configureTestingModule({ providers: [
      { provide: PatientAccountService, useValue: service },
      { provide: AuthService, useValue: { currentUser: signal(null), logout: vi.fn() } },
      { provide: Router, useValue: { navigate: vi.fn() } },
    ] });
    component = TestBed.createComponent(PortalComingSoonComponent).componentInstance;
    component.viewState.set('unlinked');
    component.startForm.setValue({ medicalRecordNumber: 'MRN-1', dateOfBirth: '1990-01-01' });
    component.verifyForm.setValue({ code: '123456' });
  });
  afterEach(() => { component.ngOnDestroy(); vi.useRealTimers(); });
  it('blocks repeated start, resend, verify and edit while start is pending', () => {
    component.onStartSubmit(); component.onStartSubmit(); component.resendCode(); component.onVerifySubmit(); component.backToStart();
    expect(service.startActivation).toHaveBeenCalledOnce(); expect(service.verifyActivation).not.toHaveBeenCalled();
    expect(component.submitting()).toBe(true);
  });
  it('keeps resend pending and blocks every competing action', () => {
    component.viewState.set('verify'); component.resendCode(); component.resendCode(); component.onVerifySubmit(); component.backToStart();
    expect(service.startActivation).toHaveBeenCalledOnce(); expect(service.verifyActivation).not.toHaveBeenCalled();
    expect(component.viewState()).toBe('verify'); expect(component.submitting()).toBe(true);
  });
  it('blocks start and resend during verification', () => {
    component.onVerifySubmit(); component.onVerifySubmit(); component.resendCode(); component.onStartSubmit();
    expect(service.verifyActivation).toHaveBeenCalledOnce(); expect(service.startActivation).not.toHaveBeenCalled();
  });
  it('uses the server deadline and edit details cannot clear it', () => {
    component.onStartSubmit(); start.next({ success: true, message: 'Requested', resendCooldownUntilUtc: '2026-10-13T00:00:17Z' });
    expect(component.cooldownSeconds()).toBe(17); component.backToStart(); component.onStartSubmit(); component.resendCode();
    expect(service.startActivation).toHaveBeenCalledOnce();
    vi.advanceTimersByTime(17000); expect(component.cooldownSeconds()).toBe(0);
    component.resendCode(); expect(service.startActivation).toHaveBeenCalledTimes(2);
  });
  it('preserves cooldown even when delivery fails', () => {
    component.onStartSubmit(); start.next({ success: false, message: 'Delivery failed', resendCooldownUntilUtc: '2026-10-13T00:00:29Z' });
    expect(component.cooldownSeconds()).toBe(29); expect(component.errorMessage()).toBe('Delivery failed');
    component.resendCode(); expect(service.startActivation).toHaveBeenCalledOnce();
  });
  it('reads start ProblemDetails detail without retry', () => {
    component.onStartSubmit(); start.error({ error: { detail: 'Refresh status before retrying.' } });
    vi.advanceTimersByTime(120000); expect(component.errorMessage()).toBe('Refresh status before retrying.');
    expect(service.startActivation).toHaveBeenCalledOnce(); expect(component.submitting()).toBe(false);
  });
  it('reads verify ProblemDetails detail without retry', () => {
    component.onVerifySubmit(); verify.error({ error: { detail: 'Unable to confirm activation.' } });
    vi.advanceTimersByTime(120000); expect(component.errorMessage()).toBe('Unable to confirm activation.');
    expect(service.verifyActivation).toHaveBeenCalledOnce(); expect(component.submitting()).toBe(false);
  });
  it('cleans up the cooldown interval on destroy', () => {
    component.onStartSubmit(); start.next({ success: true, message: 'Requested', resendCooldownUntilUtc: '2026-10-13T00:00:17Z' });
    component.ngOnDestroy(); vi.advanceTimersByTime(5000); expect(component.cooldownSeconds()).toBe(17);
  });
});
