import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import Swal from 'sweetalert2';
import { InpatientListComponent } from './inpatient-list';
import { InpatientService } from './data-access/inpatient.service';
import { Bed, Admission } from './data-access/inpatient.model';
import { PatientService } from '../patients/data-access/patient.service';
import { StaffService } from '../staff/data-access/staff.service';
import { AuthService } from '../../core/services/auth.service';

vi.mock('sweetalert2', () => ({ default: { fire: vi.fn(() => Promise.resolve({})) } }));

describe('Inpatient transition integrity UI', () => {
  let component: InpatientListComponent;
  let request: Subject<any>;
  let service: { admitPatient: ReturnType<typeof vi.fn>; transferBed: ReturnType<typeof vi.fn>;
    dischargePatient: ReturnType<typeof vi.fn>; getBedGrid: ReturnType<typeof vi.fn>; getAdmissions: ReturnType<typeof vi.fn> };
  const source: Bed = { id: 'source', wardName: 'Test', roomNumber: '1', bedNumber: '1', bedType: 'General',
    dailyRate: 100, status: 'Occupied', statusValue: 2, currentAdmissionId: 'admission-a' };
  const destination: Bed = { ...source, id: 'destination', bedNumber: '2', status: 'Available', statusValue: 1, currentAdmissionId: undefined };
  const admission: Admission = { id: 'admission-a', patientId: 'patient-a', patientName: 'Test A', patientMrn: 'TEST',
    bedId: 'source', wardName: 'Test', roomNumber: '1', bedNumber: '1', admittingDoctorName: 'Test Doctor', chiefComplaint: 'Test',
    admissionDateUtc: '2026-10-12T00:00:00Z', dailyRateSnapshot: 100, totalRoomCharge: 0, isDischarged: false };

  beforeEach(() => {
    vi.clearAllMocks(); request = new Subject<any>();
    service = { admitPatient: vi.fn(() => request), transferBed: vi.fn(() => request), dischargePatient: vi.fn(() => request),
      getBedGrid: vi.fn(() => of([source, destination])), getAdmissions: vi.fn(() => of([admission])) };
    TestBed.configureTestingModule({ providers: [
      { provide: InpatientService, useValue: service },
      { provide: PatientService, useValue: { getPatients: () => of([]) } },
      { provide: StaffService, useValue: { getStaff: () => of([]) } },
      { provide: AuthService, useValue: { hasAnyRole: () => true } },
    ] });
    component = TestBed.createComponent(InpatientListComponent).componentInstance;
    component.beds.set([source, destination]); component.admissions.set([admission]);
    component.admitForm.patchValue({ patientId: 'patient-b', bedId: 'destination', admittingDoctorName: 'Test Doctor', chiefComplaint: 'Test' });
    component.selectedAdmissionForTransfer.set(admission); component.transferForm.patchValue({ newBedId: 'destination' });
    component.selectedAdmissionForDischarge.set(admission);
  });
  afterEach(() => { document.body.style.overflow = ''; });

  function submit(operation: string): void {
    if (operation === 'admit') component.submitAdmitPatient();
    else if (operation === 'transfer') component.submitTransferPatient();
    else component.submitDischargePatient();
  }
  function mutate(operation: string): ReturnType<typeof vi.fn> {
    return operation === 'admit' ? service.admitPatient : operation === 'transfer' ? service.transferBed : service.dischargePatient;
  }

  it.each(['admit', 'transfer', 'discharge'])('blocks repeated pending %s calls without optimistic occupancy', operation => {
    submit(operation); submit(operation);
    expect(mutate(operation)).toHaveBeenCalledOnce(); expect(component.submitting()).toBe(true);
    expect(component.beds()).toEqual([source, destination]); expect(component.admissions()).toEqual([admission]);
    expect(Swal.fire).not.toHaveBeenCalled();
  });

  it.each(['admit', 'transfer', 'discharge'])('reloads authoritative beds and admissions after successful %s', operation => {
    const persisted: Admission = { ...admission, bedId: 'destination' };
    const persistedBeds = [{ ...source, status: 'Available', currentAdmissionId: undefined },
      { ...destination, status: 'Occupied', currentAdmissionId: 'admission-a' }];
    service.getBedGrid.mockReturnValue(of(persistedBeds)); service.getAdmissions.mockReturnValue(of([persisted]));
    submit(operation); request.next(operation === 'admit' ? { id: 'new-admission' } : persisted); request.complete();
    expect(service.getBedGrid).toHaveBeenCalledOnce(); expect(service.getAdmissions).toHaveBeenCalledOnce();
    expect(component.beds()).toEqual(persistedBeds); expect(component.admissions()).toEqual([persisted]);
    expect(component.submitting()).toBe(false); expect(Swal.fire).toHaveBeenCalledWith(expect.objectContaining({ icon: 'success' }));
  });

  it.each(['admit', 'transfer', 'discharge'])('shows a safe %s conflict, refreshes, and never retries automatically', operation => {
    const persisted: Admission = { ...admission, isDischarged: true, dischargeDateUtc: '2026-10-12T01:00:00Z' };
    const persistedBeds = [{ ...source, status: 'Available', currentAdmissionId: undefined }, destination];
    service.getAdmissions.mockReturnValue(of([persisted])); service.getBedGrid.mockReturnValue(of(persistedBeds));
    submit(operation); request.error({ status: 409, error: { detail: 'Bed or admission changed. Refresh before retrying.' } });
    expect(mutate(operation)).toHaveBeenCalledOnce(); expect(component.submitting()).toBe(false);
    expect(component.beds()).toEqual(persistedBeds); expect(component.admissions()).toEqual([persisted]);
    expect(component.selectedAdmissionForTransfer()).toEqual(persisted);
    expect(component.selectedAdmissionForDischarge()).toEqual(persisted);
    if (operation === 'admit') expect(component.admitError()).toContain('Bed or admission changed');
    else expect(Swal.fire).toHaveBeenCalledWith(expect.objectContaining({ icon: 'error', text: 'Bed or admission changed. Refresh before retrying.' }));
    expect(Swal.fire).not.toHaveBeenCalledWith(expect.objectContaining({ icon: 'success' }));
  });

  it('refreshes after uncertain storage failure without fabricating success', () => {
    submit('transfer'); request.error({ status: 500, error: { detail: 'Unable to confirm operation. Refresh first.' } });
    expect(service.getBedGrid).toHaveBeenCalledOnce(); expect(service.getAdmissions).toHaveBeenCalledOnce();
    expect(service.transferBed).toHaveBeenCalledOnce(); expect(component.beds()).toEqual([source, destination]);
    expect(Swal.fire).toHaveBeenCalledWith(expect.objectContaining({ icon: 'error', text: 'Unable to confirm operation. Refresh first.' }));
  });
});
