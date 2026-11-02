import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { of } from 'rxjs';
import Swal from 'sweetalert2';
import { routes } from '../../app.routes';
import { Roles } from '../../core/constants/roles';
import { authGuard } from '../../core/guards/auth.guard';
import { roleGuard } from '../../core/guards/role.guard';
import { guestGuard } from '../../core/guards/guest.guard';
import { AuthService } from '../../core/services/auth.service';
import { PatientAccountService } from '../../core/services/patient-account.service';
import { NotificationService } from '../../core/services/notification.service';
import { PatientService } from '../patients/data-access/patient.service';
import { StaffService } from '../staff/data-access/staff.service';
import { EmergencyService } from '../emergency/data-access/emergency.service';
import { AppointmentService } from '../appointments/data-access/appointment.service';
import { Shell } from '../../layout/shell/shell';
import { Login } from '../auth/login/login';

vi.mock('sweetalert2', () => ({ default: { fire: vi.fn(() => Promise.resolve({})) } }));

describe('Doctor Workspace navigation and login', () => {
  let auth: AuthService;
  let router: Router;
  let status: { getStatus: ReturnType<typeof vi.fn> };
  const shellRoute = routes.find(r => r.component === Shell)!;
  const doctorRoute = shellRoute.children!.find(r => r.path === 'doctor')!;
  function submitLogin() {
    const login = TestBed.createComponent(Login).componentInstance;
    login.loginForm.setValue({ email: 'test@example.invalid', password: 'isolated-login-test-password' });
    login.onSubmit();
  }
  function user(roles: string[], authenticated = true) {
    auth.token.set(authenticated ? 'test-token' : null);
    auth.currentUser.set(authenticated ? { id: 'account-id', firstName: 'Browser', lastName: 'Identity', email: 'test@example.invalid', roles } : null);
  }
  beforeEach(() => {
    vi.clearAllMocks(); localStorage.clear(); status = { getStatus: vi.fn(() => of({ isLinked: true })) };
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([]),
      { provide: PatientAccountService, useValue: status },
      { provide: NotificationService, useValue: { unreadCount: signal(0), notifications: signal([]), init: vi.fn(), stop: vi.fn() } },
      { provide: PatientService, useValue: {} }, { provide: StaffService, useValue: {} },
      { provide: EmergencyService, useValue: {} }, { provide: AppointmentService, useValue: {} },
    ] });
    auth = TestBed.inject(AuthService); router = TestBed.inject(Router); vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });
  afterEach(() => localStorage.clear());
  it('defines a lazy Doctor route with authentication and exact Doctor role requirements', () => {
    expect(shellRoute.canActivate).toContain(authGuard); expect(doctorRoute.canActivate).toEqual([authGuard, roleGuard]);
    expect(doctorRoute.data?.['roles']).toEqual([Roles.Doctor]); expect(doctorRoute.loadComponent).toBeTypeOf('function');
  });
  it.each([[Roles.Doctor], [Roles.Admin, Roles.Doctor]])('allows the Doctor UX for %j', (...roles) => {
    user(roles); expect(TestBed.runInInjectionContext(() => roleGuard({ data: doctorRoute.data } as ActivatedRouteSnapshot, {} as RouterStateSnapshot))).toBe(true);
  });
  it.each([Roles.Admin, Roles.Nurse, Roles.Receptionist, Roles.Pharmacist, Roles.LabTechnician, Roles.BillingStaff, Roles.Patient])('denies Doctor navigation and route access to %s', (role) => {
    user([role]); const shell = TestBed.createComponent(Shell).componentInstance;
    expect(shell.visibleNavItems.some(i => i.route === '/doctor')).toBe(false);
    const result = TestBed.runInInjectionContext(() => roleGuard({ data: doctorRoute.data } as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
    expect(router.serializeUrl(result as UrlTree)).toBe(role === Roles.Patient ? '/portal' : '/forbidden');
  });
  it('redirects anonymous users to login', () => {
    user([], false); expect(router.serializeUrl(TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)) as UrlTree)).toBe('/login');
  });
  it('makes Workspace the primary Doctor sidebar destination while keeping existing module links', () => {
    user([Roles.Doctor]); const shell = TestBed.createComponent(Shell).componentInstance;
    expect(shell.clinicalNavItems[0].route).toBe('/doctor'); expect(shell.clinicalNavItems[0].label).toBe('Doctor Workspace');
    expect(shell.visibleNavItems.some(i => i.route === '/dashboard')).toBe(false); expect(shell.visibleNavItems.some(i => i.route === '/appointments')).toBe(true);
  });
  it('keeps Admin dashboard precedence while offering Admin+Doctor its own Workspace', () => {
    user([Roles.Admin, Roles.Doctor]); const shell = TestBed.createComponent(Shell).componentInstance;
    expect(auth.getStaffHomeRoute()).toBe('/dashboard'); expect(shell.visibleNavItems.some(i => i.route === '/doctor')).toBe(true); expect(shell.visibleNavItems.some(i => i.route === '/dashboard')).toBe(true);
  });
  it.each([Roles.Admin, Roles.Nurse, Roles.Receptionist, Roles.Pharmacist, Roles.LabTechnician, Roles.BillingStaff])('retains /dashboard as the non-Doctor staff home for %s', (role) => {
    user([role]); expect(auth.getStaffHomeRoute()).toBe('/dashboard');
  });
  it('redirects authenticated Doctor guests to Workspace without introducing a login loop', () => {
    user([Roles.Doctor]); const result = TestBed.runInInjectionContext(() => guestGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
    expect(router.serializeUrl(result as UrlTree)).toBe('/doctor');
    user([], false); expect(TestBed.runInInjectionContext(() => guestGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot))).toBe(true);
  });
  it.each([[Roles.Doctor, '/doctor'], [Roles.Admin, '/dashboard'], [Roles.Nurse, '/dashboard'], [Roles.Receptionist, '/dashboard'], [Roles.Pharmacist, '/dashboard'], [Roles.LabTechnician, '/dashboard'], [Roles.BillingStaff, '/dashboard']])('routes successful %s login to %s', (role, destination) => {
    user([role]); vi.spyOn(auth, 'login').mockReturnValue(of({ firstName: 'Identity' } as any));
    submitLogin(); expect(router.navigate).toHaveBeenCalledWith([destination]); expect(status.getStatus).not.toHaveBeenCalled(); expect(Swal.fire).toHaveBeenCalled();
  });
  it('preserves Admin precedence on Admin+Doctor login', () => {
    user([Roles.Admin, Roles.Doctor]); vi.spyOn(auth, 'login').mockReturnValue(of({} as any)); submitLogin(); expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });
  it.each([[true, '/portal'], [false, '/portal-activation']])('preserves Patient-only login/link-status behavior (%s)', (linked, destination) => {
    user([Roles.Patient]); status.getStatus.mockReturnValue(of({ isLinked: linked })); vi.spyOn(auth, 'login').mockReturnValue(of({} as any));
    submitLogin(); expect(router.navigate).toHaveBeenCalledWith([destination]); expect(status.getStatus).toHaveBeenCalledOnce();
  });
  it('starts without demo credentials and rejects an empty submission', () => {
    const fixture = TestBed.createComponent(Login);
    const login = fixture.componentInstance;
    const authenticate = vi.spyOn(auth, 'login');
    expect(login.loginForm.value).toEqual({ email: '', password: '' });
    fixture.detectChanges();
    const submit = fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement;
    expect(submit.textContent).toContain('Sign in to Health ERP');
    expect(submit.disabled).toBe(true);
    login.onSubmit();
    expect(login.loginForm.invalid).toBe(true);
    expect(authenticate).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    login.errorMessage.set('Authentication failed for the test account.');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('p-message')?.textContent).toContain('Authentication failed for the test account.');
  });
  it('preserves existing non-Doctor route roles and module destinations', () => {
    const byPath = (path: string) => shellRoute.children!.find(r => r.path === path)!;
    expect(byPath('staff').data?.['roles']).toEqual([Roles.Admin]); expect(byPath('billing').data?.['roles']).toEqual([Roles.Admin, Roles.BillingStaff]);
    expect(byPath('patients').data?.['roles']).toContain(Roles.Nurse); expect(byPath('appointments').data?.['roles']).toContain(Roles.Receptionist);
    expect(byPath('laboratory').data?.['roles']).toContain(Roles.LabTechnician); expect(byPath('pharmacy').data?.['roles']).toContain(Roles.Pharmacist);
    expect(routes.find(r => r.path === 'portal')?.data?.['roles']).toEqual([Roles.Patient]);
  });
});
