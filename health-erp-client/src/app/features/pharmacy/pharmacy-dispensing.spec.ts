import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import Swal from 'sweetalert2';
import { PharmacyListComponent } from './pharmacy-list';
import { PharmacyService } from './data-access/pharmacy.service';
import { Prescription } from './data-access/pharmacy.model';
import { PatientService } from '../patients/data-access/patient.service';
import { StaffService } from '../staff/data-access/staff.service';
import { AuthService } from '../../core/services/auth.service';

vi.mock('sweetalert2', () => ({ default: { fire: vi.fn(() => Promise.resolve({})) } }));

describe('Pharmacy dispensing UI', () => {
  let component: PharmacyListComponent;
  let request: Subject<Prescription>;
  let service: { dispensePrescription: ReturnType<typeof vi.fn>; getInventory: ReturnType<typeof vi.fn>;
    getPrescriptions: ReturnType<typeof vi.fn> };
  const pending: Prescription = { id: 'rx-a', patientId: 'patient-a', patientName: 'Test Patient', drugId: 'drug-a',
    doctorName: 'Test Doctor', medicationName: 'Test medication', dosage: 'Text', frequency: 'Text',
    durationDays: 7, refills: 0, status: 'Pending', prescribedDateUtc: '2026-10-12T00:00:00Z' };

  beforeEach(() => {
    vi.clearAllMocks(); request = new Subject<Prescription>();
    service = { dispensePrescription: vi.fn(() => request), getInventory: vi.fn(() => of([])),
      getPrescriptions: vi.fn(() => of([pending])) };
    TestBed.configureTestingModule({ providers: [
      { provide: PharmacyService, useValue: service },
      { provide: PatientService, useValue: { getPatients: () => of([]) } },
      { provide: StaffService, useValue: { getStaff: () => of([]) } },
      { provide: AuthService, useValue: { hasAnyRole: () => true } },
    ] });
    component = TestBed.createComponent(PharmacyListComponent).componentInstance;
    component.prescriptions.set([pending]);
  });

  it('keeps pending state until success and reloads inventory from the backend', () => {
    const dispensed = { ...pending, status: 'Dispensed', dispensedDateUtc: '2026-10-12T01:00:00Z' };
    service.getPrescriptions.mockReturnValue(of([dispensed]));
    service.getInventory.mockReturnValue(of([{ id: 'drug-a', stockQuantity: 7 }]));
    component.dispensePrescription(pending);
    expect(component.prescriptions()[0].status).toBe('Pending'); expect(component.submitting()).toBe(true);
    request.next(dispensed); request.complete();
    expect(component.prescriptions()[0].status).toBe('Dispensed'); expect(component.submitting()).toBe(false);
    expect(component.drugs()[0].stockQuantity).toBe(7);
    expect(service.getInventory).toHaveBeenCalledOnce();
  });

  it('shows the stock/conflict message and refreshes without faking a successful dispense', () => {
    service.getInventory.mockReturnValue(of([{ id: 'drug-a', stockQuantity: 2 }]));
    component.dispensePrescription(pending);
    request.error({ status: 409, error: { detail: 'Insufficient inventory for the requested dispense quantity.' } });
    expect(component.prescriptions()[0].status).toBe('Pending'); expect(component.submitting()).toBe(false);
    expect(component.drugs()[0].stockQuantity).toBe(2);
    expect(Swal.fire).toHaveBeenCalledWith(expect.objectContaining({ icon: 'error', text: 'Insufficient inventory for the requested dispense quantity.' }));
  });

  it('blocks duplicate clicks while pending and ignores terminal prescriptions', () => {
    component.dispensePrescription(pending); component.dispensePrescription(pending);
    expect(service.dispensePrescription).toHaveBeenCalledOnce();
    request.error({ status: 500 });
    component.dispensePrescription({ ...pending, status: 'Dispensed' });
    expect(service.dispensePrescription).toHaveBeenCalledOnce();
    expect(component.prescriptions()[0].status).toBe('Pending');
  });

  it('allows retry after a controlled failure', () => {
    component.dispensePrescription(pending); request.error({ status: 409, error: { detail: 'Refresh and retry.' } });
    service.dispensePrescription.mockReturnValue(new Subject<Prescription>());
    component.dispensePrescription(pending);
    expect(service.dispensePrescription).toHaveBeenCalledTimes(2); expect(component.submitting()).toBe(true);
  });

  it('leaves 401 handling to the existing session interceptor', () => {
    component.dispensePrescription(pending); request.error({ status: 401 });
    expect(component.submitting()).toBe(false); expect(Swal.fire).not.toHaveBeenCalled();
    expect(component.prescriptions()[0].status).toBe('Pending');
  });
});
