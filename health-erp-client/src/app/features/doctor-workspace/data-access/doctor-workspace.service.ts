import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { DoctorClinicalNoteInput, DoctorPatientSnapshot, DoctorSchedule } from './doctor-workspace.model';

@Injectable({ providedIn: 'root' })
export class DoctorWorkspaceService {
  private readonly http = inject(HttpClient);

  getSchedule(date?: string) {
    const params = date ? new HttpParams().set('date', date) : new HttpParams();
    return this.http.get<DoctorSchedule>('/api/v1/doctor/schedule', { params });
  }

  getSnapshot(patientId: string) {
    return this.http.get<DoctorPatientSnapshot>(`/api/v1/doctor/patients/${encodeURIComponent(patientId)}/snapshot`);
  }

  recordClinicalNote(patientId: string, input: DoctorClinicalNoteInput) {
    return this.http.post<{ noteId: string; recordedAtUtc: string }>(`/api/v1/doctor/patients/${encodeURIComponent(patientId)}/clinical-notes`, input);
  }
}
