import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Staff, CreateStaffRequest, StaffAccountLookup, StaffAccountLink, StaffKind } from './staff.model';

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

    lookupAccounts(search: string, page = 1): Observable<StaffAccountLookup> {
        const params = new HttpParams().set('search', search).set('page', page).set('pageSize', 20);
        return this.http.get<StaffAccountLookup>(`${this.baseUrl}/account-lookup`, { params });
    }

    linkAccount(staffId: string, userId: string, staffKind: StaffKind): Observable<StaffAccountLink> {
        return this.http.post<StaffAccountLink>(`${this.baseUrl}/${staffId}/account-link`, { userId, staffKind });
    }

    unlinkAccount(staffId: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/${staffId}/account-link`);
    }
}
