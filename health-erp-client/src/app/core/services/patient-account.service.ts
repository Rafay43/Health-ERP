import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface LinkedPatientSummary {
  mrn: string;
  displayName: string;
}

export interface PatientAccountStatus {
  isLinked: boolean;
  patient?: LinkedPatientSummary | null;
}

export interface StartActivationResponse {
  success: boolean;
  message: string;
  maskedDestination?: string | null;
  requiresSupport?: boolean;
  resendCooldownUntilUtc?: string | null;
}

export interface VerifyActivationResponse {
  success: boolean;
  message: string;
  patient?: LinkedPatientSummary | null;
}

@Injectable({
  providedIn: 'root',
})
export class PatientAccountService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/patient-account';

  getStatus(): Observable<PatientAccountStatus> {
    return this.http.get<PatientAccountStatus>(`${this.baseUrl}/status`);
  }

  startActivation(medicalRecordNumber: string, dateOfBirth: string): Observable<StartActivationResponse> {
    return this.http.post<StartActivationResponse>(`${this.baseUrl}/activation/start`, {
      medicalRecordNumber,
      dateOfBirth,
    });
  }

  verifyActivation(code: string): Observable<VerifyActivationResponse> {
    return this.http.post<VerifyActivationResponse>(`${this.baseUrl}/activation/verify`, { code });
  }
}
