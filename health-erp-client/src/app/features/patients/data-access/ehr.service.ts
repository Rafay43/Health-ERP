import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PatientEhr, AddClinicalNoteRequest } from './ehr.model';

@Injectable({ providedIn: 'root' })
export class EhrService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/patients';

  getPatientEhr(patientId: string): Observable<PatientEhr> {
    return this.http.get<PatientEhr>(`${this.baseUrl}/${patientId}/ehr`);
  }

  addClinicalNote(patientId: string, request: AddClinicalNoteRequest): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/${patientId}/clinical-notes`, request);
  }
}
