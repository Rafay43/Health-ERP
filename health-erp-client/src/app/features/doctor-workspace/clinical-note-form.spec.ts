import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DoctorWorkspace } from './doctor-workspace';
import { By } from '@angular/platform-browser';
import { DoctorClinicalNoteForm } from './clinical-note-form';
import { DoctorPatientSnapshot, DoctorSchedule } from './data-access/doctor-workspace.model';

describe('Doctor clinical note recording', () => {
  let fixture: ComponentFixture<DoctorWorkspace>; let workspace: DoctorWorkspace; let form: DoctorClinicalNoteForm; let http: HttpTestingController;
  const item = { appointmentId: 'appointment-a', patientId: 'patient-a', patientDisplayName: 'Real Patient', patientMedicalRecordNumber: 'MRN-a', startDateTimeUtc: '2026-10-22T07:00:00Z', endDateTimeUtc: '2026-10-22T07:30:00Z', status: 'Scheduled' as const, reason: 'Recorded' };
  const schedule: DoctorSchedule = { date: '2026-10-22', timeZoneId: 'Asia/Karachi', doctor: { displayName: 'Verified Doctor', specialty: '', department: '' }, summary: { total: 1, scheduled: 1, confirmed: 0, completed: 0, cancelled: 0, noShow: 0 }, nextScheduledConsultation: item, appointments: [item] };
  const snapshot = (): DoctorPatientSnapshot => ({ patient: { patientId: 'patient-a', displayName: 'Real Patient', medicalRecordNumber: 'MRN-a', dateOfBirth: '1990-01-01', gender: null }, latestVitals: null, recentNotes: [], recentPrescriptions: [], recentLabOrders: [], currentAdmission: null });
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [DoctorWorkspace], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController); fixture = TestBed.createComponent(DoctorWorkspace); workspace = fixture.componentInstance;
    fixture.detectChanges(); http.expectOne('/api/v1/doctor/schedule').flush(schedule); workspace.openSnapshot(item);
    http.expectOne('/api/v1/doctor/patients/patient-a/snapshot').flush(snapshot()); fixture.detectChanges();
  });
  afterEach(() => { fixture.destroy(); http.verify({ ignoreCancelled: true }); });
  function openForm() {
    const button = [...document.querySelectorAll('button')].find(b => b.textContent?.trim() === 'Add Clinical Note')!;
    button.click(); fixture.detectChanges();
    form = fixture.debugElement.query(By.directive(DoctorClinicalNoteForm)).componentInstance;
  }
  it('offers only the focused clinical-note action and no other clinical writes', () => {
    const labels = [...document.querySelectorAll('button')].map(b => b.textContent?.trim()); expect(labels).toContain('Add Clinical Note');
    for (const label of ['Prescribe Medication','Order Lab','Complete Appointment','Edit Patient','Admit','Transfer','Discharge']) expect(labels).not.toContain(label);
  });
  it('starts all fields empty with no author, timestamp or unsafe legacy defaults', () => {
    openForm(); expect(form.form.getRawValue()).toEqual({ chiefComplaint: '', diagnosis: '', notes: '', bloodPressure: '', heartRateBpm: null, temperatureCelsius: null, weightKg: null });
    expect(Object.keys(form.form.controls)).not.toEqual(expect.arrayContaining(['doctorName', 'staffId', 'userId', 'noteDateUtc']));
    for (const field of document.querySelectorAll('input, textarea')) expect((field as HTMLInputElement).value).toBe('');
    expect(form.form.invalid).toBe(true); form.submit(); http.expectNone(r => r.method === 'POST');
  });
  it('rejects whitespace-only content', () => { openForm(); form.form.patchValue({ notes: ' \n ', bloodPressure: ' ' }); expect(form.form.invalid).toBe(true); form.submit(); http.expectNone(r => r.method === 'POST'); });
  it.each(['notes','chiefComplaint','diagnosis'])('allows %s-only content without requiring diagnosis or vitals', field => {
    openForm(); form.form.get(field)!.setValue('Actual recorded content'); expect(form.form.valid).toBe(true); form.submit();
    const r = http.expectOne('/api/v1/doctor/patients/patient-a/clinical-notes'); expect(r.request.method).toBe('POST'); expect(r.request.params.keys()).toEqual([]);
    expect(Object.keys(r.request.body).sort()).toEqual(['chiefComplaint','diagnosis','notes','bloodPressure','heartRateBpm','temperatureCelsius','weightKg'].sort());
    expect(r.request.body[field]).toBe('Actual recorded content');
  });
  it('allows vitals-only submission with the exact clinical-only request', () => {
    openForm(); form.form.patchValue({ heartRateBpm: 91 }); expect(form.form.valid).toBe(true); form.submit();
    const r = http.expectOne('/api/v1/doctor/patients/patient-a/clinical-notes'); expect(r.request.body).toEqual({ chiefComplaint: null, diagnosis: null, notes: null, bloodPressure: null, heartRateBpm: 91, temperatureCelsius: null, weightKg: null });
  });
  it.each(['normal','999/99','80/90','139 / 91'])('rejects malformed/out-of-range BP %s and shows feedback', bp => {
    openForm(); form.form.patchValue({ bloodPressure: bp }); form.form.controls.bloodPressure.markAsTouched(); fixture.detectChanges();
    expect(form.form.invalid).toBe(true); expect(document.body.textContent).toContain('Enter a valid systolic/diastolic'); form.submit(); http.expectNone(r => r.method === 'POST');
  });
  it.each([['heartRateBpm',0],['heartRateBpm',301],['heartRateBpm',1.5],['temperatureCelsius',24],['temperatureCelsius',46],['weightKg',0],['weightKg',-1],['weightKg',501],['weightKg',61.234]])('blocks invalid %s value %s instead of treating it as missing', (field, value) => {
    openForm(); form.form.get(field as string)!.setValue(value); expect(form.form.invalid).toBe(true); form.submit(); http.expectNone(r => r.method === 'POST');
  });
  it('blocks duplicate submission and Patient/date/drawer changes while saving', () => {
    openForm(); form.form.patchValue({ notes: 'Real note' }); form.submit(); form.submit();
    http.expectOne('/api/v1/doctor/patients/patient-a/clinical-notes'); expect(workspace.noteSaving()).toBe(true);
    workspace.openSnapshot({ ...item, patientId: 'patient-b' }); workspace.moveDay(1); workspace.loadSchedule(); workspace.closeSnapshot(); form.cancel();
    expect(workspace.snapshot()?.patient.patientId).toBe('patient-a'); expect(workspace.snapshotOpen()).toBe(true); expect(workspace.noteFormOpen()).toBe(true);
    http.expectNone('/api/v1/doctor/patients/patient-b/snapshot'); http.expectNone('/api/v1/doctor/schedule');
  });
  it('closes/resets after a committed response and re-fetches the secure Snapshot without local insertion', () => {
    openForm(); form.form.patchValue({ notes: 'Real note', heartRateBpm: 91 }); form.submit();
    const post = http.expectOne('/api/v1/doctor/patients/patient-a/clinical-notes'); post.flush({ noteId: 'note-server', recordedAtUtc: '2026-10-22T12:00:00Z' });
    expect(workspace.noteFormOpen()).toBe(false); expect(form.form.controls.notes.value).toBe(''); expect(workspace.snapshot()).toBeNull();
    const refresh = http.expectOne('/api/v1/doctor/patients/patient-a/snapshot');
    const updated = snapshot(); updated.recentNotes = [{ noteId: 'note-server', recordedAtUtc: '2026-10-22T12:00:00Z', chiefComplaint: '', diagnosis: '', notes: 'Server-persisted note', recordedByDisplayName: 'Verified Server Author' }];
    updated.latestVitals = { bloodPressure: null, heartRateBpm: 91, temperatureCelsius: null, weightKg: null, recordedAtUtc: '2026-10-22T12:00:00Z' }; refresh.flush(updated); fixture.detectChanges();
    expect(document.body.textContent).toContain('Server-persisted note'); expect(document.body.textContent).toContain('Recorded by Verified Server Author'); expect(document.body.textContent).toContain('91 bpm');
    expect(workspace.noteSuccess()).toContain('Clinical note saved');
  });
  it.each([400,401,403,404,409,500])('retains clinical content on %s and never reveals server internals', status => {
    openForm(); form.form.patchValue({ notes: 'Keep this content', weightKg: 61.25 }); form.submit();
    http.expectOne('/api/v1/doctor/patients/patient-a/clinical-notes').flush({ detail: 'PRIVATE SQL internals' }, { status, statusText: 'Error' }); fixture.detectChanges();
    expect(form.form.controls.notes.value).toBe('Keep this content'); expect(workspace.noteFormOpen()).toBe(true); expect(workspace.noteSaving()).toBe(false); expect(form.error()).not.toContain('PRIVATE SQL');
    if (status === 404) expect(form.error()).toBe('Patient is not available.');
  });
  it('does not allow a late success refresh to overwrite another Patient', () => {
    openForm(); form.form.patchValue({ notes: 'Recorded' }); form.submit(); http.expectOne('/api/v1/doctor/patients/patient-a/clinical-notes').flush({ noteId: 'note-a' });
    const old = http.expectOne('/api/v1/doctor/patients/patient-a/snapshot'); workspace.openSnapshot({ ...item, patientId: 'patient-b' });
    expect(old.cancelled).toBe(true); const current = http.expectOne('/api/v1/doctor/patients/patient-b/snapshot'); current.flush({ ...snapshot(), patient: { ...snapshot().patient, patientId: 'patient-b' } });
    expect(workspace.snapshot()?.patient.patientId).toBe('patient-b');
  });
  it('cancels an unsaved form without sending any write', () => { openForm(); form.form.patchValue({ notes: 'Unsaved' }); form.cancel(); expect(workspace.noteFormOpen()).toBe(false); http.expectNone(r => r.method === 'POST'); });
});
