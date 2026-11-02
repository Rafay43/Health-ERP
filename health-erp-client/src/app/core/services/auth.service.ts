import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  ForgotPasswordRequest,
  ForgotPasswordResponse,
  ResetPasswordRequest,
  ResetPasswordResponse,
  User,
} from '../models/auth.models';

const TOKEN_KEY = 'health_erp_token';
const USER_KEY = 'health_erp_user';

import { Roles, UserRole } from '../constants/roles';

function parseRolesFromJwt(token: string | null): string[] {
  if (!token) return [];
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return [];
    const payloadJson = atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'));
    const payload = JSON.parse(payloadJson);

    const roleClaimKey = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
    const rawRoles = payload[roleClaimKey] || payload['role'] || payload['roles'] || [];

    if (Array.isArray(rawRoles)) {
      return rawRoles;
    } else if (typeof rawRoles === 'string') {
      return [rawRoles];
    }
    return [];
  } catch {
    return [];
  }
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private baseUrl = '/api/v1/auth';

  // Reactive State via Signals
  readonly currentUser = signal<User | null>(this.getStoredUser());
  readonly token = signal<string | null>(this.getStoredToken());

  readonly isAuthenticated = computed(() => !!this.token() && !!this.currentUser());

  readonly userRoles = computed<string[]>(() => {
    const tokenVal = this.token();
    const jwtRoles = parseRolesFromJwt(tokenVal);
    if (jwtRoles.length > 0) {
      return jwtRoles;
    }
    return this.currentUser()?.roles || [];
  });

  getCurrentUserRoles(): string[] {
    return this.userRoles();
  }

  hasRole(role: string): boolean {
    return this.userRoles().includes(role);
  }

  hasAnyRole(roles: string[]): boolean {
    if (!roles || roles.length === 0) return true;
    const currentRoles = this.userRoles();
    return roles.some((r) => currentRoles.includes(r));
  }

  isAdmin(): boolean {
    return this.hasRole(Roles.Admin);
  }

  isPatient(): boolean {
    return this.hasRole(Roles.Patient) && !this.isAdmin() && this.userRoles().length === 1;
  }

  getStaffHomeRoute(): string {
    if (this.isAdmin()) return '/dashboard';
    return this.hasRole(Roles.Doctor) ? '/doctor' : '/dashboard';
  }

  getPrimaryRoleDisplay(): string {
    const roles = this.userRoles();
    if (roles.includes(Roles.Admin)) return 'System Administrator';
    if (roles.includes(Roles.Doctor)) return 'Doctor';
    if (roles.includes(Roles.Nurse)) return 'Nurse';
    if (roles.includes(Roles.Pharmacist)) return 'Pharmacist';
    if (roles.includes(Roles.LabTechnician)) return 'Lab Technician';
    if (roles.includes(Roles.Receptionist)) return 'Receptionist';
    if (roles.includes(Roles.BillingStaff)) return 'Billing Staff';
    if (roles.includes(Roles.Patient)) return 'Patient';
    return roles[0] || 'User';
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/register`, request)
      .pipe(tap((res) => this.handleAuthSuccess(res)));
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/login`, request)
      .pipe(tap((res) => this.handleAuthSuccess(res)));
  }

  forgotPassword(email: string): Observable<ForgotPasswordResponse> {
    return this.http.post<ForgotPasswordResponse>(`${this.baseUrl}/forgot-password`, { email });
  }

  resetPassword(request: ResetPasswordRequest): Observable<ResetPasswordResponse> {
    return this.http.post<ResetPasswordResponse>(`${this.baseUrl}/reset-password`, request);
  }

  logout(): void {
    const hadSession = !!this.token() || !!this.currentUser();
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.token.set(null);
    this.currentUser.set(null);
    if (hadSession) this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.token();
  }

  private handleAuthSuccess(res: AuthResponse): void {
    const user: User = {
      id: res.id,
      email: res.email,
      firstName: res.firstName,
      lastName: res.lastName,
      roles: res.roles,
    };

    localStorage.setItem(TOKEN_KEY, res.token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));

    this.token.set(res.token);
    this.currentUser.set(user);
  }

  private getStoredToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  private getStoredUser(): User | null {
    const stored = localStorage.getItem(USER_KEY);
    if (!stored) return null;
    try {
      return JSON.parse(stored) as User;
    } catch {
      return null;
    }
  }
}
