import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { DoctorWorkspace } from './doctor-workspace';
import { DoctorPatientSnapshot, DoctorSchedule, DoctorScheduleAppointment, ScheduleStatus } from './data-access/doctor-workspace.model';

const appointment = (id = 'appointment-a', patientId = 'patient-a', status: ScheduleStatus = 'Scheduled'): DoctorScheduleAppointment => ({
  appointmentId: id, patientId, patientDisplayName: 'Recorded Patient ' + patientId, patientMedicalRecordNumber: 'MRN-' + patientId,
  startDateTimeUtc: '2026-10-22T07:00:00Z', endDateTimeUtc: '2026-10-22T07:30:00Z', status, reason: 'Persisted consultation reason',
});
const schedule = (): DoctorSchedule => ({
  date: '2026-10-22', timeZoneId: 'Asia/Karachi', doctor: { displayName: 'Verified Staff Name', specialty: 'Cardiology', department: 'Outpatient' },
  summary: { total: 5, scheduled: 1, confirmed: 1, completed: 1, cancelled: 1, noShow: 1 },
  nextScheduledConsultation: appointment('backend-next', 'next-patient', 'Confirmed'),
  appointments: (['Scheduled', 'Confirmed', 'Completed', 'Cancelled', 'NoShow'] as ScheduleStatus[]).map((s, i) => appointment('appointment-' + i, 'patient-' + i, s)),
});
const snapshot = (patientId = 'patient-0'): DoctorPatientSnapshot => ({
  patient: { patientId, displayName: 'Snapshot ' + patientId, medicalRecordNumber: 'RECORDED-MRN', dateOfBirth: '1987-04-03', gender: 'Female' },
  latestVitals: null, recentNotes: [], recentPrescriptions: [], recentLabOrders: [], currentAdmission: null,
});

describe('Doctor Workspace', () => {
  let fixture: ComponentFixture<DoctorWorkspace>;
  let component: DoctorWorkspace;
  let http: HttpTestingController;
  let initial: TestRequest;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [DoctorWorkspace], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController); fixture = TestBed.createComponent(DoctorWorkspace); component = fixture.componentInstance;
    fixture.detectChanges(); initial = http.expectOne('/api/v1/doctor/schedule');
  });
  afterEach(() => { fixture.destroy(); http.verify({ ignoreCancelled: true }); });
  const text = () => (fixture.nativeElement.textContent + (document.querySelector('.p-drawer')?.textContent ?? '')) as string;
  function render(data = schedule()) { initial.flush(data); fixture.detectChanges(); }
  function open(data = snapshot(), item = schedule().appointments[0]) {
    component.openSnapshot(item); fixture.detectChanges();
    const request = http.expectOne('/api/v1/doctor/patients/' + item.patientId + '/snapshot'); request.flush(data); fixture.detectChanges();
  }

  it('initially requests only the dedicated server-default schedule with no ownership or date parameters', () => {
    expect(initial.request.method).toBe('GET'); expect(initial.request.params.keys()).toEqual([]);
    expect(component.schedule()).toBeNull(); expect(component.loading()).toBe(true); expect(text()).toContain('Loading your schedule');
  });
  it('renders authoritative Staff identity, facility date/timezone and real summary values', () => {
    render(); expect(text()).toContain('Dr. Verified Staff Name'); expect(text()).toContain('Cardiology'); expect(text()).toContain('Outpatient');
    expect(text()).toContain('October 22, 2026'); expect(text()).toContain('Asia/Karachi');
    expect(fixture.nativeElement.querySelector('[data-testid="metric-Total Appointments"]').textContent).toContain('5');
    expect(fixture.nativeElement.querySelector('[data-testid="metric-Completed"]').textContent).toContain('1');
  });
  it('preserves all genuine zero counts with an empty list and null next consultation', () => {
    const data = schedule(); data.summary = { total: 0, scheduled: 0, confirmed: 0, completed: 0, cancelled: 0, noShow: 0 };
    data.appointments = []; data.nextScheduledConsultation = null; render(data);
    for (const metric of fixture.nativeElement.querySelectorAll('.metric-value')) expect(metric.textContent.trim()).toBe('0');
    expect(text()).toContain('No appointments scheduled'); expect(text()).toContain('No upcoming scheduled consultation for this date.');
  });
  it('never displays general dashboard metrics, fake trends or fabricated statuses', () => {
    render(); expect(text()).not.toMatch(/revenue|occupancy|growth|waiting|queue|checked.?in|in.?progress/i);
    expect(text()).not.toContain('84%'); expect(text()).not.toContain('48,500');
  });
  it('uses the backend-selected next record even when another schedule item appears earlier', () => {
    const data = schedule(); data.appointments[0].startDateTimeUtc = '2026-10-22T06:00:00Z'; render(data);
    const next = fixture.nativeElement.querySelector('[data-testid="next-consultation"]'); expect(next.textContent).toContain('next-patient');
    next.querySelector('button').click(); expect(http.expectOne('/api/v1/doctor/patients/next-patient/snapshot').request.method).toBe('GET');
  });
  it('renders schedule rows in the exact backend order', () => {
    render(); const rows = [...fixture.nativeElement.querySelectorAll('[data-testid="schedule-row"]')] as HTMLElement[];
    expect(rows.length).toBe(5); rows.forEach((row, i) => { expect(row.textContent).toContain('patient-' + i); expect(row.textContent).toContain('MRN-patient-' + i); expect(row.textContent).toContain('Persisted consultation reason'); });
  });
  it.each([['Scheduled', 'Scheduled'], ['Confirmed', 'Confirmed'], ['Completed', 'Completed'], ['Cancelled', 'Cancelled'], ['NoShow', 'No Show']])('renders real %s appointment status', (status, label) => {
    render(); expect(fixture.nativeElement.querySelector('.schedule-row .' + component.statusClass(status)).textContent).toBe(label);
  });
  it('uses neutral unavailable text for missing schedule Patient identity', () => {
    const data = schedule(); data.appointments[0].patientDisplayName = null; data.appointments[0].patientMedicalRecordNumber = null; render(data);
    expect(fixture.nativeElement.querySelector('.schedule-row').textContent).toContain('Unavailable');
  });
  it('View Snapshot uses only the selected PatientId from a schedule row', () => {
    render(); fixture.nativeElement.querySelector('.schedule-row button').click();
    const request = http.expectOne('/api/v1/doctor/patients/patient-0/snapshot');
    expect(request.request.method).toBe('GET'); expect(request.request.params.keys()).toEqual([]); expect(request.request.body).toBeNull();
  });
  it('renders persisted Snapshot identity and honest missing-data messages', () => {
    render(); open(); expect(text()).toContain('Snapshot patient-0'); expect(text()).toContain('RECORDED-MRN'); expect(text()).toContain('April 3, 1987'); expect(text()).toContain('Female');
    for (const message of ['No recorded vitals available.', 'No recent clinical notes.', 'No recent prescriptions.', 'No recent lab activity.', 'No current admission.']) expect(text()).toContain(message);
    expect(text()).not.toContain('120/80'); expect(text()).not.toContain('72 bpm'); expect(text()).not.toContain('37 °C'); expect(text()).not.toContain('70 kg');
  });
  it('preserves partial vitals and real numeric zeros without inventing missing values', () => {
    render(); const data = snapshot(); data.latestVitals = { bloodPressure: null, heartRateBpm: 0, temperatureCelsius: null, weightKg: 61.4, recordedAtUtc: '2026-10-21T19:30:00Z' }; open(data);
    expect(text()).toContain('0 bpm'); expect(text()).toContain('61.4 kg'); expect(text()).toContain('Not recorded'); expect(text()).not.toContain('120/80');
    expect(text()).toContain('Oct 22, 2026');
  });
  it('renders actual clinical notes without inventing author attribution', () => {
    render(); const data = snapshot(); data.recentNotes = [{ noteId: 'note-a', recordedAtUtc: '2026-10-22T07:00:00Z', chiefComplaint: 'Actual complaint', diagnosis: 'Actual diagnosis', notes: '<script>untrusted</script> recorded text', recordedByDisplayName: null }]; open(data);
    const notes = document.querySelector('#notes-heading')!.parentElement!;
    expect(notes.textContent).toContain('Actual diagnosis'); expect(notes.textContent).toContain('<script>untrusted</script>'); expect(notes.querySelector('script')).toBeNull(); expect(notes.textContent).not.toContain('Dr.');
  });
  it('labels and renders Recent Prescriptions without claims of active medication use', () => {
    render(); const data = snapshot(); data.recentPrescriptions = [{ prescriptionId: 'rx-a', prescribedDateUtc: '2026-10-22T07:00:00Z', medicationName: 'Recorded medicine', dosage: '5 mg', frequency: 'Twice daily', durationDays: 7, refills: 0, instructions: 'Actual instructions', status: 'Cancelled', dispensedDateUtc: null }]; open(data);
    expect(text()).toContain('Recent Prescriptions'); expect(text()).toContain('Recorded medicine'); expect(text()).toContain('Actual instructions'); expect(text()).not.toContain('Active Medications');
  });
  it('does not fabricate a null lab result', () => {
    render(); const data = snapshot(); data.recentLabOrders = [{ labOrderId: 'lab-a', orderedDateUtc: '2026-10-22T07:00:00Z', testName: 'Recorded test', testCode: 'TEST', priority: 'Routine', status: 'Ordered', result: null }]; open(data);
    expect(text()).toContain('Recent Lab Orders / Results'); expect(text()).toContain('Recorded test'); expect(text()).toContain('No recorded result available.'); expect(document.querySelector('.lab-result')).toBeNull();
  });
  it('shows supplied lab results and stored abnormal flags without medical interpretation', () => {
    render(); const data = snapshot(); data.recentLabOrders = [{ labOrderId: 'lab-a', orderedDateUtc: '2026-10-22T07:00:00Z', testName: 'Recorded test', testCode: 'TEST', priority: 'Urgent', status: 'Completed', result: { completedDateUtc: '2026-10-22T08:00:00Z', value: '9.4', referenceRange: '4–8', unit: 'unit', remarks: 'Recorded remarks', isAbnormal: true } }]; open(data);
    expect(text()).toContain('9.4'); expect(text()).toContain('Recorded abnormal flag'); expect(text()).toContain('Recorded remarks'); expect(text()).not.toContain('Normal');
  });
  it('renders only supplied current admission information', () => {
    render(); const data = snapshot(); data.currentAdmission = { admissionId: 'admission-a', admissionDateUtc: '2026-10-21T07:00:00Z', chiefComplaint: 'Persisted admission complaint' }; open(data);
    expect(text()).toContain('Persisted admission complaint'); expect(text()).not.toMatch(/room charge|daily rate/i);
  });
  it.each([404, 403, 500, 401])('handles Snapshot %s with safe wording and no clinical fallback', (status) => {
    render(); component.openSnapshot(schedule().appointments[0]);
    http.expectOne('/api/v1/doctor/patients/patient-0/snapshot').flush({ detail: 'PRIVATE SQL Patient exists but is assigned elsewhere' }, { status, statusText: 'Error' }); fixture.detectChanges();
    expect(component.snapshot()).toBeNull(); expect(text()).not.toContain('PRIVATE SQL'); expect(text()).not.toContain('assigned elsewhere');
    if (status === 404) expect(text()).toContain('Patient snapshot is not available.');
  });
  it.each([500, 403, 401])('handles schedule %s without retaining stale data or supplying fallbacks', (status) => {
    initial.flush({ detail: 'Private database error' }, { status, statusText: 'Error' }); fixture.detectChanges();
    expect(component.schedule()).toBeNull(); expect(text()).toContain('Schedule unavailable'); expect(text()).not.toContain('Private database error'); expect(fixture.nativeElement.querySelector('.metric')).toBeNull();
  });
  it('retries the last requested date and never sends ownership parameters', () => {
    render(); component.moveDay(1); const failed = http.expectOne('/api/v1/doctor/schedule?date=2026-10-23'); failed.flush({}, { status: 500, statusText: 'Error' });
    component.retrySchedule(); const retry = http.expectOne('/api/v1/doctor/schedule?date=2026-10-23'); expect(retry.request.params.keys()).toEqual(['date']);
  });
  it('Today always reloads the server-default schedule, including across facility midnight', () => {
    render(); component.moveDay(1); http.expectOne('/api/v1/doctor/schedule?date=2026-10-23').flush({ ...schedule(), date: '2026-10-23' });
    component.loadSchedule(); const today = http.expectOne('/api/v1/doctor/schedule'); expect(today.request.params.keys()).toEqual([]);
    today.flush({ ...schedule(), date: '2026-10-24' }); expect(component.selectedDate()).toBe('2026-10-24'); expect(component.facilityToday()).toBe('2026-10-24');
  });
  it('uses calendar arithmetic across month/year boundaries independently of browser timezone', () => {
    render({ ...schedule(), date: '2026-12-31' }); component.moveDay(1);
    http.expectOne('/api/v1/doctor/schedule?date=2027-01-01'); component.moveDay(-1); http.expectOne('/api/v1/doctor/schedule?date=2026-12-31');
    expect(component.calendarDate('1987-04-03')).toBe('April 3, 1987');
  });
  it('cancels stale rapid date requests so only the newest date can render', () => {
    render(); component.moveDay(1); const old = http.expectOne('/api/v1/doctor/schedule?date=2026-10-23'); component.moveDay(1);
    const latest = http.expectOne('/api/v1/doctor/schedule?date=2026-10-24'); expect(old.cancelled).toBe(true);
    expect(() => old.flush({ ...schedule(), date: '2026-10-23' })).toThrow(); latest.flush({ ...schedule(), date: '2026-10-24' }); fixture.detectChanges();
    expect(component.schedule()?.date).toBe('2026-10-24'); expect(text()).toContain('October 24, 2026');
  });
  it('clears the previous Patient immediately and cancels an older pending Patient request', () => {
    render(); open(); component.openSnapshot(schedule().appointments[1]); fixture.detectChanges(); expect(component.snapshot()).toBeNull(); expect(text()).not.toContain('Snapshot patient-0');
    const old = http.expectOne('/api/v1/doctor/patients/patient-1/snapshot'); component.openSnapshot(schedule().appointments[2]);
    const latest = http.expectOne('/api/v1/doctor/patients/patient-2/snapshot'); expect(old.cancelled).toBe(true); latest.flush(snapshot('patient-2')); fixture.detectChanges();
    expect(text()).toContain('Snapshot patient-2'); expect(text()).not.toContain('Snapshot patient-0');
  });
  it('clears and cancels Snapshot state on date change and close', () => {
    render(); open(); component.openSnapshot(schedule().appointments[1]); const pending = http.expectOne('/api/v1/doctor/patients/patient-1/snapshot'); component.moveDay(1);
    expect(pending.cancelled).toBe(true); expect(component.snapshotOpen()).toBe(false); expect(component.snapshot()).toBeNull(); http.expectOne('/api/v1/doctor/schedule?date=2026-10-23');
  });
  it('cleans up pending requests when destroyed', () => {
    render(); component.openSnapshot(schedule().appointments[0]); const pending = http.expectOne('/api/v1/doctor/patients/patient-0/snapshot'); fixture.destroy(); expect(pending.cancelled).toBe(true);
  });
  it('has read-only controls, keyboard-labeled date navigation and missing-state loading indicators', () => {
    render(); open(); expect(text()).toContain('Read-only clinical snapshot');
    expect(text()).not.toMatch(/Add Note|Prescribe\s*<|Order Lab|Complete Appointment|Reschedule|Edit Patient|Discharge|Transfer|Admit Patient/);
    expect(fixture.nativeElement.querySelector('button[aria-label="Previous day"]')).not.toBeNull(); expect(fixture.nativeElement.querySelector('button[aria-label="Next day"]')).not.toBeNull();
    expect(document.querySelector('input, textarea, select',)).toBeNull();
  });
  it('formats UTC appointment times in the API facility timezone', () => {
    render(); expect(component.time('2026-10-21T19:30:00Z')).toBe('12:30 AM'); expect(component.recorded('2026-10-21T19:30:00Z')).toContain('Oct 22, 2026');
  });
});
