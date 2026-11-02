import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { PatientPortalService } from '../../core/services/patient-portal.service';
import { PortalHealthRecordComponent } from './health-record/portal-health-record';
import { PortalPrescriptionsComponent } from './prescriptions/portal-prescriptions';
import { PortalOverviewComponent } from './overview/portal-overview';
import { PortalAppointmentsComponent } from './appointments/portal-appointments';
import { PortalBillingComponent } from './billing/portal-billing';
import { PortalLabResultsComponent } from './lab-results/portal-lab-results';
import { PortalProfileComponent } from './profile/portal-profile';

// Raw JSON field names match the portal DTOs serialized with ASP.NET web defaults.
const recordedDate = '2026-10-11T10:00:00Z';
const vitals = {
  bloodPressure: '120/80', heartRateBpm: 72, temperatureCelsius: 37,
  weightKg: 70, recordedDateUtc: recordedDate,
};
const profile = {
  mrn: 'MRN-0009', firstName: 'Portal', lastName: 'Patient', displayName: 'Portal Patient',
  dateOfBirth: '1990-01-01', age: 36, gender: 'Male', phoneNumber: null, email: 'portal@example.com',
};
const appointment = {
  id: 'appointment-1', startDateTimeUtc: recordedDate, endDateTimeUtc: '2026-10-11T10:30:00Z',
  doctorName: 'Dr. Portal', specialty: 'General Medicine', department: 'OutPatient',
  reason: 'Annual Checkup', status: 'Scheduled',
};
const prescription = {
  id: 'prescription-1', medicationName: 'Atorvastatin 20mg', dosage: '500mg', frequency: 'Twice daily',
  durationDays: 7, refills: 0, instructions: 'After meals', doctorName: 'Dr. Portal',
  status: 'Pending', prescribedDateUtc: recordedDate, dispensedDateUtc: null,
};

describe('Patient Portal API contract rendering', () => {
  let service: {
    getProfile: ReturnType<typeof vi.fn>;
    getDashboard: ReturnType<typeof vi.fn>;
    getHealthRecord: ReturnType<typeof vi.fn>;
    getPrescriptions: ReturnType<typeof vi.fn>;
    getAppointments: ReturnType<typeof vi.fn>;
    getBilling: ReturnType<typeof vi.fn>;
    getLabResults: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    service = {
      getProfile: vi.fn(() => of(profile)),
      getDashboard: vi.fn(() => of({
        profile, nextAppointment: appointment, latestVitals: vitals,
        activePrescriptionCount: 1, recentLabResultCount: 1, outstandingBalance: 150,
      })),
      getHealthRecord: vi.fn(() => of({
        latestVitals: vitals,
        encounters: [{ id: 'encounter-1', noteDateUtc: recordedDate, doctorName: 'Dr. Portal',
          chiefComplaint: 'Annual Checkup', diagnosis: 'Routine examination' }],
      })),
      getPrescriptions: vi.fn(() => of([prescription])),
      getAppointments: vi.fn(() => of([appointment])),
      getBilling: vi.fn(() => of([{
        id: 'invoice-1', invoiceNumber: 'INV-0009', issueDateUtc: recordedDate,
        dueDateUtc: '2026-11-11T10:00:00Z', status: 'Pending', totalAmount: 150,
        paidAmount: 0, balanceDue: 150,
        lineItems: [{ description: 'Medical Consultation', quantity: 1, unitPrice: 150, totalPrice: 150 }],
        payments: [],
      }])),
      getLabResults: vi.fn(() => of([{
        id: 'lab-1', testName: 'CBC', testCode: 'CBC', priority: 'Routine', orderingDoctorName: 'Dr. Portal',
        orderedDateUtc: recordedDate, status: 'Completed', resultValue: 'normal', referenceRange: 'Standard Range',
        unit: null, performedBy: 'Technician', completedDateUtc: recordedDate, isAbnormal: false,
      }])),
    };
  });

  async function render<T>(component: Type<T>) {
    await TestBed.configureTestingModule({
      imports: [component],
      providers: [provideRouter([]), { provide: PatientPortalService, useValue: service }],
    }).compileComponents();
    const fixture = TestBed.createComponent(component);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  function text(fixture: { nativeElement: HTMLElement }): string {
    return fixture.nativeElement.textContent?.replace(/\s+/g, ' ').trim() || '';
  }

  it('renders actual health-record vitals and safe encounters from canonical JSON', async () => {
    const fixture = await render(PortalHealthRecordComponent);
    const content = text(fixture);
    for (const value of ['120/80 mmHg', '72 bpm', '37 °C', '70 kg', 'Oct 11, 2026',
      'Dr. Portal', 'Annual Checkup', 'Routine examination']) {
      expect(content).toContain(value);
    }
    expect(content).not.toContain('No recent vital signs');
    expect(content).not.toContain('No clinical consultation history');
  });

  it('renders N/A for unrecorded values without inventing measurements', async () => {
    service.getHealthRecord.mockReturnValue(of({
      latestVitals: { bloodPressure: null, heartRateBpm: null, temperatureCelsius: null, weightKg: null,
        recordedDateUtc: recordedDate }, encounters: [],
    }));
    const fixture = await render(PortalHealthRecordComponent);
    expect(text(fixture).match(/N\/A/g)?.length).toBe(4);
    expect(text(fixture)).not.toContain('120/80');
    expect(text(fixture)).toContain('No clinical consultation history');
  });

  it('keeps honest empty health-record states', async () => {
    service.getHealthRecord.mockReturnValue(of({ latestVitals: null, encounters: [] }));
    const fixture = await render(PortalHealthRecordComponent);
    expect(text(fixture)).toContain('No recent vital signs');
    expect(text(fixture)).toContain('No clinical consultation history');
  });

  it('does not render extra internal provider notes even if present in a response', async () => {
    service.getHealthRecord.mockReturnValue(of({ latestVitals: null, encounters: [{
      id: 'encounter-1', noteDateUtc: recordedDate, doctorName: 'Dr. Portal', chiefComplaint: 'Checkup',
      diagnosis: 'Routine examination', notes: 'SECRET_INTERNAL_PROVIDER_NOTE',
    }] }));
    const fixture = await render(PortalHealthRecordComponent);
    expect(text(fixture)).toContain('Routine examination');
    expect(text(fixture)).not.toContain('SECRET_INTERNAL_PROVIDER_NOTE');
  });

  it('renders prescription doctor, numeric duration and existing medication details', async () => {
    const fixture = await render(PortalPrescriptionsComponent);
    for (const value of ['Atorvastatin 20mg', 'Dr. Portal', '7 days', '500mg', 'Twice daily', 'After meals', 'Pending']) {
      expect(text(fixture)).toContain(value);
    }
  });

  it('renders missing prescription fields safely without clinical fallback values', async () => {
    service.getPrescriptions.mockReturnValue(of([{ ...prescription, doctorName: '', durationDays: 0,
      dosage: '', frequency: '', instructions: null }]));
    const fixture = await render(PortalPrescriptionsComponent);
    expect(text(fixture)).toContain('Not recorded');
    expect(text(fixture)).toContain('N/A');
    expect(text(fixture)).not.toContain('Dr. Portal');
    expect(text(fixture)).not.toContain('Daily');
  });

  it('uses actual Pending, Dispensed and Cancelled prescription status values in filters', async () => {
    service.getPrescriptions.mockReturnValue(of([prescription,
      { ...prescription, id: 'dispensed', status: 'Dispensed' },
      { ...prescription, id: 'cancelled', status: 'Cancelled' }]));
    const fixture = await render(PortalPrescriptionsComponent);
    fixture.componentInstance.activeTab.set('active');
    fixture.detectChanges();
    expect(fixture.componentInstance.filteredPrescriptions().map(rx => rx.status)).toEqual(['Pending', 'Dispensed']);
    fixture.componentInstance.activeTab.set('completed');
    fixture.detectChanges();
    expect(fixture.componentInstance.filteredPrescriptions().map(rx => rx.status)).toEqual(['Cancelled']);
  });

  it('uses dashboard vitals without fetching a second health-record source', async () => {
    const fixture = await render(PortalOverviewComponent);
    expect(text(fixture)).toContain('120/80');
    expect(text(fixture)).toContain('72 bpm');
    expect(text(fixture)).toContain('Dr. Portal');
    expect(text(fixture)).toContain('$150.00');
    expect(service.getDashboard).toHaveBeenCalledOnce();
    expect(service.getHealthRecord).not.toHaveBeenCalled();
  });

  it('preserves appointment reason, department and status and displays doctorName', async () => {
    const fixture = await render(PortalAppointmentsComponent);
    for (const value of ['Annual Checkup', 'OutPatient', 'Scheduled', 'Dr. Portal']) {
      expect(text(fixture)).toContain(value);
    }
  });

  it('preserves billing balances and renders the canonical item totalPrice', async () => {
    const fixture = await render(PortalBillingComponent);
    expect(text(fixture)).toContain('Pending');
    expect(text(fixture)).toContain('Balance Due');
    const itemLabel = Array.from(fixture.nativeElement.querySelectorAll('span') as NodeListOf<HTMLSpanElement>)
      .find(span => span.textContent?.trim() === 'Medical Consultation');
    const row = itemLabel?.parentElement?.parentElement;
    expect(row?.lastElementChild?.textContent?.trim()).toBe('$150.00');
    expect(row?.textContent).toContain('Qty: 1');
  });

  it('preserves released lab-result rendering', async () => {
    const fixture = await render(PortalLabResultsComponent);
    for (const value of ['CBC', 'normal', 'Standard Range', 'Oct 11, 2026', 'Dr. Portal']) {
      expect(text(fixture)).toContain(value);
    }
  });

  it('preserves profile rendering', async () => {
    const fixture = await render(PortalProfileComponent);
    expect(text(fixture)).toContain('MRN-0009');
    expect(text(fixture)).toContain('Portal Patient');
    expect(text(fixture)).toContain('portal@example.com');
  });
});
