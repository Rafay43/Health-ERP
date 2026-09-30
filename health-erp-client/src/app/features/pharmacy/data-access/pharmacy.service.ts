import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Drug, Prescription, CreateDrugRequest, CreatePrescriptionRequest } from './pharmacy.model';

@Injectable({ providedIn: 'root' })
export class PharmacyService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/pharmacy';

  getInventory(searchTerm?: string, category?: string, lowStockOnly?: boolean, expiringSoonOnly?: boolean): Observable<Drug[]> {
    let params = new HttpParams();
    if (searchTerm) params = params.set('searchTerm', searchTerm);
    if (category) params = params.set('category', category);
    if (lowStockOnly) params = params.set('lowStockOnly', 'true');
    if (expiringSoonOnly) params = params.set('expiringSoonOnly', 'true');

    return this.http.get<Drug[]>(`${this.baseUrl}/inventory`, { params });
  }

  createDrug(request: CreateDrugRequest): Observable<Drug> {
    return this.http.post<Drug>(`${this.baseUrl}/inventory`, request);
  }

  updateStock(drugId: string, newQuantity: number): Observable<Drug> {
    return this.http.patch<Drug>(`${this.baseUrl}/inventory/${drugId}/stock`, { newQuantity });
  }

  getPrescriptions(patientId?: string, status?: string): Observable<Prescription[]> {
    let params = new HttpParams();
    if (patientId) params = params.set('patientId', patientId);
    if (status) params = params.set('status', status);

    return this.http.get<Prescription[]>(`${this.baseUrl}/prescriptions`, { params });
  }

  createPrescription(request: CreatePrescriptionRequest): Observable<Prescription> {
    return this.http.post<Prescription>(`${this.baseUrl}/prescriptions`, request);
  }

  dispensePrescription(prescriptionId: string, quantityToDeduct: number = 1): Observable<Prescription> {
    return this.http.post<Prescription>(`${this.baseUrl}/prescriptions/${prescriptionId}/dispense`, { quantityToDeduct });
  }
}
