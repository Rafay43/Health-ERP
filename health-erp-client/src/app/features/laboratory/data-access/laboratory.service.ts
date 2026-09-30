import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  LabTest,
  LabOrder,
  CreateLabTestRequest,
  CreateLabOrderRequest,
  RecordLabResultRequest
} from './laboratory.model';

@Injectable({ providedIn: 'root' })
export class LaboratoryService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/laboratory';

  getCatalog(searchTerm?: string, category?: string): Observable<LabTest[]> {
    let params = new HttpParams();
    if (searchTerm) params = params.set('searchTerm', searchTerm);
    if (category) params = params.set('category', category);

    return this.http.get<LabTest[]>(`${this.baseUrl}/catalog`, { params });
  }

  createLabTest(request: CreateLabTestRequest): Observable<LabTest> {
    return this.http.post<LabTest>(`${this.baseUrl}/catalog`, request);
  }

  getOrders(patientId?: string, status?: string, priority?: string, abnormalOnly?: boolean): Observable<LabOrder[]> {
    let params = new HttpParams();
    if (patientId) params = params.set('patientId', patientId);
    if (status) params = params.set('status', status);
    if (priority) params = params.set('priority', priority);
    if (abnormalOnly) params = params.set('abnormalOnly', 'true');

    return this.http.get<LabOrder[]>(`${this.baseUrl}/orders`, { params });
  }

  createOrder(request: CreateLabOrderRequest): Observable<LabOrder> {
    return this.http.post<LabOrder>(`${this.baseUrl}/orders`, request);
  }

  recordResult(orderId: string, request: RecordLabResultRequest): Observable<LabOrder> {
    return this.http.post<LabOrder>(`${this.baseUrl}/orders/${orderId}/results`, request);
  }
}
