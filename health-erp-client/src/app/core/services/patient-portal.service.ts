import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface PatientPortalProfileDto {
  mrn: string;
  firstName: string;
  lastName: string;
  displayName: string;
  dateOfBirth: string;
  age: number;
  gender?: string | null;
  phoneNumber?: string | null;
  email?: string | null;
}

export interface PatientAppointmentDto {
  id: string;
  startDateTimeUtc: string;
  endDateTimeUtc: string;
  doctorName: string;
  department: string | null;
  reason: string;
  status: string;
}

export interface PatientVitalDto {
  bloodPressure: string | null;
  heartRateBpm: number | null;
  temperatureCelsius: number | null;
  weightKg: number | null;
  recordedDateUtc: string;
}

export interface PatientEncounterDto {
  id: string;
  noteDateUtc: string;
  doctorName: string;
  chiefComplaint: string;
  diagnosis: string;
}

export interface PatientHealthRecordDto {
  latestVitals: PatientVitalDto | null;
  encounters: PatientEncounterDto[];
}

export interface PatientPrescriptionDto {
  id: string;
  medicationName: string;
  dosage: string;
  frequency: string;
  durationDays: number;
  instructions: string | null;
  prescribedDateUtc: string;
  doctorName: string;
  status: string;
}

export interface PatientLabResultDto {
  id: string;
  testName: string;
  testCode: string;
  orderedDateUtc: string;
  completedDateUtc?: string | null;
  resultValue?: string | null;
  referenceRange?: string | null;
  unit?: string | null;
  isAbnormal: boolean;
  status: string;
  orderingDoctorName: string;
}

export interface PatientInvoiceLineItemDto {
  description: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface PatientPaymentDto {
  amount: number;
  paymentMethod: string;
  paymentDateUtc: string;
}

export interface PatientInvoiceDto {
  id: string;
  invoiceNumber: string;
  issueDateUtc: string;
  dueDateUtc: string;
  status: string;
  totalAmount: number;
  paidAmount: number;
  balanceDue: number;
  lineItems: PatientInvoiceLineItemDto[];
  payments: PatientPaymentDto[];
}

export interface PatientDashboardDto {
  nextAppointment?: PatientAppointmentDto | null;
  latestVitals?: PatientVitalDto | null;
  activePrescriptionCount: number;
  recentLabResultCount: number;
  outstandingBalance: number;
}

@Injectable({
  providedIn: 'root',
})
export class PatientPortalService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/patient-portal';

  getProfile(): Observable<PatientPortalProfileDto> {
    return this.http.get<PatientPortalProfileDto>(`${this.baseUrl}/me`);
  }

  getDashboard(): Observable<PatientDashboardDto> {
    return this.http.get<PatientDashboardDto>(`${this.baseUrl}/me/dashboard`);
  }

  getAppointments(page = 1, pageSize = 25, status?: string): Observable<PatientAppointmentDto[]> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<PatientAppointmentDto[]>(`${this.baseUrl}/me/appointments`, { params });
  }

  getHealthRecord(page = 1, pageSize = 25): Observable<PatientHealthRecordDto> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PatientHealthRecordDto>(`${this.baseUrl}/me/health-record`, { params });
  }

  getPrescriptions(page = 1, pageSize = 25, status?: string): Observable<PatientPrescriptionDto[]> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<PatientPrescriptionDto[]>(`${this.baseUrl}/me/prescriptions`, { params });
  }

  getLabResults(page = 1, pageSize = 25): Observable<PatientLabResultDto[]> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PatientLabResultDto[]>(`${this.baseUrl}/me/lab-results`, { params });
  }

  getBilling(page = 1, pageSize = 25, status?: string): Observable<PatientInvoiceDto[]> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<PatientInvoiceDto[]>(`${this.baseUrl}/me/billing`, { params });
  }
}
