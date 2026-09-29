import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Appointment, BookAppointmentRequest } from './appointment.model';

@Injectable({ providedIn: 'root' })
export class AppointmentService {
    private http = inject(HttpClient);
    private baseUrl = '/api/v1/appointments';

    getAppointments(): Observable<Appointment[]> {
        return this.http.get<Appointment[]>(this.baseUrl);
    }

    bookAppointment(request: BookAppointmentRequest): Observable<{ id: string }> {
        return this.http.post<{ id: string }>(this.baseUrl, request);
    }
}
