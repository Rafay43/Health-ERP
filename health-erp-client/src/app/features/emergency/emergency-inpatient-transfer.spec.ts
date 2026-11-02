import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import Swal from 'sweetalert2';
import { EmergencyListComponent } from './emergency-list';
import { EmergencyService } from './data-access/emergency.service';
import { TriageRecord } from './data-access/emergency.model';
import { InpatientService } from '../inpatient/data-access/inpatient.service';
import { PatientService } from '../patients/data-access/patient.service';
import { StaffService } from '../staff/data-access/staff.service';
import { AuthService } from '../../core/services/auth.service';

vi.mock('sweetalert2', () => ({ default: { fire: vi.fn(() => Promise.resolve({})) } }));

describe('Emergency inpatient bed acquisition UI', () => {
  let component: EmergencyListComponent;
  let request: Subject<any>;
  let service: { transferToInpatient: ReturnType<typeof vi.fn>; getEmergencyQueue: ReturnType<typeof vi.fn> };
  let beds: { getBedGrid: ReturnType<typeof vi.fn> };
  const triage: TriageRecord = { id: 'triage-a', patientId: 'patient-a', patientName: 'Test A', patientMrn: 'TEST',
    chiefComplaint: 'Test', bloodPressure: '120/80', heartRateBpm: 70, oxygenSaturationSpO2: 98,
    bodyTemperatureCelsius: 37, respiratoryRateBpm: 16, severityName: 'Level5_NonUrgent', severityLevel: 5,
    statusName: 'Triaged', statusValue: 1, triageNurseName: 'Test Nurse', arrivalDateUtc: '2026-10-12T00:00:00Z' };
  beforeEach(() => {
    vi.clearAllMocks(); request = new Subject<any>();
    service = { transferToInpatient: vi.fn(() => request), getEmergencyQueue: vi.fn(() => of([triage])) };
    beds = { getBedGrid: vi.fn(() => of([])) };
    TestBed.configureTestingModule({ providers: [
      { provide: EmergencyService, useValue: service }, { provide: InpatientService, useValue: beds },
      { provide: PatientService, useValue: { getPatients: () => of([]) } },
      { provide: StaffService, useValue: { getStaff: () => of([]) } },
      { provide: AuthService, useValue: { hasAnyRole: () => true } },
    ] });
    component = TestBed.createComponent(EmergencyListComponent).componentInstance;
    component.triageRecords.set([triage]); component.openTransferModal(triage);
    component.transferForm.patchValue({ bedId: 'bed-x' });
  });
  afterEach(() => { document.body.style.overflow = ''; });
  it('guards pending duplicates and reloads queue and beds after a conflict without retrying', () => {
    component.submitTransfer(); component.submitTransfer(); expect(service.transferToInpatient).toHaveBeenCalledOnce();
    expect(component.triageRecords()).toEqual([triage]);
    request.error({ status: 409, error: { detail: 'Selected bed is not available.' } });
    expect(service.getEmergencyQueue).toHaveBeenCalledOnce(); expect(beds.getBedGrid).toHaveBeenCalledOnce();
    expect(component.submitting()).toBe(false); expect(service.transferToInpatient).toHaveBeenCalledOnce();
    expect(Swal.fire).toHaveBeenCalledWith(expect.objectContaining({ icon: 'error', text: 'Selected bed is not available.' }));
  });
  it('reloads authoritative state after success', () => {
    service.getEmergencyQueue.mockReturnValue(of([])); component.submitTransfer(); request.next({ admissionId: 'admission-a' }); request.complete();
    expect(component.triageRecords()).toEqual([]); expect(beds.getBedGrid).toHaveBeenCalledOnce();
    expect(component.showTransferDialog()).toBe(false); expect(component.submitting()).toBe(false);
    expect(Swal.fire).toHaveBeenCalledWith(expect.objectContaining({ icon: 'success' }));
  });
});
