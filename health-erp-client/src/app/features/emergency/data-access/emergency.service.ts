import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  TriageRecord,
  CreateTriageRecordRequest,
  AssignBayRequest,
  TransferInpatientRequest
} from './emergency.model';

@Injectable({ providedIn: 'root' })
export class EmergencyService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/emergency';

  getEmergencyQueue(activeOnly: boolean = true): Observable<TriageRecord[]> {
    let params = new HttpParams().set('activeOnly', activeOnly ? 'true' : 'false');
    return this.http.get<TriageRecord[]>(`${this.baseUrl}/queue`, { params });
  }

  createTriageRecord(request: CreateTriageRecordRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/triage`, request);
  }

  assignBay(triageId: string, request: AssignBayRequest): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.baseUrl}/triage/${triageId}/assign-bay`, request);
  }

  transferToInpatient(triageId: string, request: TransferInpatientRequest): Observable<{ admissionId: string }> {
    return this.http.post<{ admissionId: string }>(`${this.baseUrl}/triage/${triageId}/transfer-inpatient`, request);
  }

  dischargeFromEmergency(triageId: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.baseUrl}/triage/${triageId}/discharge`, {});
  }
}
