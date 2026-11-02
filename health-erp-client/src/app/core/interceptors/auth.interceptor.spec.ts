import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';
import { authGuard } from '../guards/auth.guard';

describe('Revoked session handling', () => {
  let auth: AuthService;
  let client: HttpClient;
  let http: HttpTestingController;
  let router: { navigate: ReturnType<typeof vi.fn>; createUrlTree: ReturnType<typeof vi.fn> };
  const user = { id: 'user-a', email: 'a@example.invalid', firstName: 'Test', lastName: 'User', roles: ['Doctor'] };
  const token = (version: string) => 'header.' + btoa(JSON.stringify({ role: 'Doctor', session_version: version })) + '.signature';

  beforeEach(() => {
    localStorage.clear();
    router = { navigate: vi.fn(() => Promise.resolve(true)), createUrlTree: vi.fn(() => 'login-tree') };
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(),
      { provide: Router, useValue: router },
    ] });
    auth = TestBed.inject(AuthService); client = TestBed.inject(HttpClient); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); localStorage.clear(); });

  function login(version = 'version-a') {
    auth.login({ email: user.email, password: 'test-password' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush({ ...user, token: token(version) });
  }
  function get(url: string) { client.get(url).subscribe({ error: () => {} }); }
  function fail401(url: string) {
    http.expectOne(url).flush({}, { status: 401, statusText: 'Unauthorized' });
  }

  it('clears credentials and stale roles on 401 and redirects once', () => {
    login(); expect(auth.getCurrentUserRoles()).toEqual(['Doctor']);
    get('/api/v1/protected');
    const request = http.expectOne('/api/v1/protected');
    expect(request.request.headers.get('Authorization')).toBe('Bearer ' + token('version-a'));
    request.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.isAuthenticated()).toBe(false); expect(auth.currentUser()).toBeNull();
    expect(auth.getCurrentUserRoles()).toEqual([]);
    expect(localStorage.getItem('health_erp_token')).toBeNull();
    expect(localStorage.getItem('health_erp_user')).toBeNull();
    expect(router.navigate).toHaveBeenCalledExactlyOnceWith(['/login']);
    expect(TestBed.runInInjectionContext(() => authGuard({} as any, {} as any))).toBe('login-tree');
  });

  it('does not repeatedly logout or redirect for simultaneous rejected requests', () => {
    login(); const logout = vi.spyOn(auth, 'logout');
    get('/api/v1/one'); get('/api/v1/two');
    fail401('/api/v1/one'); fail401('/api/v1/two');
    expect(logout).toHaveBeenCalledOnce(); expect(router.navigate).toHaveBeenCalledOnce();
  });

  it('does not let a late 401 from an old token end a newly authenticated session', () => {
    login(); get('/api/v1/old-request');
    login('version-b'); fail401('/api/v1/old-request');
    expect(auth.getToken()).toBe(token('version-b')); expect(auth.isAuthenticated()).toBe(true);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('preserves ordinary anonymous login failures without redirect loops', () => {
    auth.login({ email: user.email, password: 'wrong-password' }).subscribe({ error: () => {} });
    fail401('/api/v1/auth/login');
    expect(auth.isAuthenticated()).toBe(false); expect(router.navigate).not.toHaveBeenCalled();
  });

  it('keeps a valid authenticated session when a policy rejects it with 403', () => {
    login(); get('/api/v1/admin/users');
    http.expectOne('/api/v1/admin/users').flush({}, { status: 403, statusText: 'Forbidden' });
    expect(auth.isAuthenticated()).toBe(true); expect(auth.getCurrentUserRoles()).toEqual(['Doctor']);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('makes explicit logout idempotent and rejects stale stored role state', () => {
    login(); auth.logout(); auth.logout();
    expect(router.navigate).toHaveBeenCalledOnce(); expect(auth.getCurrentUserRoles()).toEqual([]);
    get('/api/v1/after-logout');
    const request = http.expectOne('/api/v1/after-logout');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(router.navigate).toHaveBeenCalledOnce();
  });
});
