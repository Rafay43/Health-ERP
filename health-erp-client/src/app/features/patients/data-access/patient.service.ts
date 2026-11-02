import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { RegisterPatientRequest, RegisterPatientResponse, Patient, NextMrnResponse, UpdatePatientRequest } from './patient.model';

@Injectable({ providedIn: 'root' })
export class PatientService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/patients';

  registerPatient(request: RegisterPatientRequest): Observable<RegisterPatientResponse> {
    return this.http.post<RegisterPatientResponse>(this.baseUrl, request);
  }

  updatePatient(id: string, request: UpdatePatientRequest): Observable<Patient> {
    return this.http.put<Patient>(`${this.baseUrl}/${id}`, request);
  }

  getPatients(): Observable<Patient[]> {
    return this.http.get<Patient[]>(this.baseUrl);
  }

  getNextMrn(): Observable<NextMrnResponse> {
    return this.http.get<NextMrnResponse>(`${this.baseUrl}/next-mrn`);
  }
}