import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Bed,
  Admission,
  CreateBedRequest,
  AdmitPatientRequest,
  DischargeRequest
} from './inpatient.model';

@Injectable({ providedIn: 'root' })
export class InpatientService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/inpatient';

  getBedGrid(wardName?: string): Observable<Bed[]> {
    let params = new HttpParams();
    if (wardName) params = params.set('wardName', wardName);
    return this.http.get<Bed[]>(`${this.baseUrl}/beds`, { params });
  }

  createBed(request: CreateBedRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/beds`, request);
  }

  getAdmissions(patientId?: string, activeOnly?: boolean): Observable<Admission[]> {
    let params = new HttpParams();
    if (patientId) params = params.set('patientId', patientId);
    if (activeOnly !== undefined) params = params.set('activeOnly', activeOnly ? 'true' : 'false');
    return this.http.get<Admission[]>(`${this.baseUrl}/admissions`, { params });
  }

  admitPatient(request: AdmitPatientRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/admissions`, request);
  }

  dischargePatient(admissionId: string, request: DischargeRequest): Observable<Admission> {
    return this.http.post<Admission>(`${this.baseUrl}/admissions/${admissionId}/discharge`, request);
  }
}
