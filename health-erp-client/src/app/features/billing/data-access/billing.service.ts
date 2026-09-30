import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Invoice, RecordPaymentRequest, CreateInvoiceRequest } from './billing.model';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/invoices';

  getInvoices(status?: string, patientId?: string): Observable<Invoice[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (patientId) params = params.set('patientId', patientId);

    return this.http.get<Invoice[]>(this.baseUrl, { params });
  }

  createInvoice(request: CreateInvoiceRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, request);
  }

  recordPayment(invoiceId: string, request: RecordPaymentRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${invoiceId}/payments`, request);
  }
}
