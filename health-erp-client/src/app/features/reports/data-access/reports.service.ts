import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ExecutiveDashboardMetrics } from './reports.model';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/reports';

  getExecutiveDashboardMetrics(): Observable<ExecutiveDashboardMetrics> {
    return this.http.get<ExecutiveDashboardMetrics>(`${this.baseUrl}/dashboard`);
  }

  downloadInvoicesCsv(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/export/invoices`, { responseType: 'blob' });
  }
}
