import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Staff, CreateStaffRequest } from './staff.model';

@Injectable({ providedIn: 'root' })
export class StaffService {
    private http = inject(HttpClient);
    private baseUrl = '/api/v1/staff';

    getStaff(): Observable<Staff[]> {
        return this.http.get<Staff[]>(this.baseUrl);
    }

    createStaff(request: CreateStaffRequest): Observable<{ id: string }> {
        return this.http.post<{ id: string }>(this.baseUrl, request);
    }
}
