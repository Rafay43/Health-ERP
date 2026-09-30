import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { UserAccount, AuditLogEntry, SystemHealth } from './admin.model';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/admin';

  getUsers(): Observable<UserAccount[]> {
    return this.http.get<UserAccount[]>(`${this.baseUrl}/users`);
  }

  updateUserRole(userId: string, roleName: string): Observable<UserAccount> {
    return this.http.put<UserAccount>(`${this.baseUrl}/users/${userId}/role`, { roleName });
  }

  toggleUserStatus(userId: string, enableAccount: boolean): Observable<UserAccount> {
    return this.http.patch<UserAccount>(`${this.baseUrl}/users/${userId}/status`, { enableAccount });
  }

  getAuditLogs(entityName?: string): Observable<AuditLogEntry[]> {
    let params = new HttpParams();
    if (entityName) params = params.set('entityName', entityName);

    return this.http.get<AuditLogEntry[]>(`${this.baseUrl}/audit-logs`, { params });
  }

  getSystemHealth(): Observable<SystemHealth> {
    return this.http.get<SystemHealth>(`${this.baseUrl}/health`);
  }
}
