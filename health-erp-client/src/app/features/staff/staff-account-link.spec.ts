import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import Swal from 'sweetalert2';
import { StaffList } from './staff-list';
import { StaffService } from './data-access/staff.service';
import { AuthService } from '../../core/services/auth.service';
import { Staff, StaffAccountCandidate, StaffKind } from './data-access/staff.model';

vi.mock('sweetalert2', () => ({ default: { fire: vi.fn(() => Promise.resolve({ isConfirmed: true })) } }));

describe('Admin Staff account linking', () => {
  let component: StaffList;
  let admin: boolean;
  let lookup: Subject<any>;
  let link: Subject<any>;
  let unlink: Subject<void>;
  let service: any;
  const staff: Staff = { id: 'staff-a', firstName: 'Recorded', lastName: 'Staff', email: 'staff@example.invalid', phoneNumber: '',
    specialty: 'General', licenseNumber: '', department: 'Outpatient', isActive: true, staffKind: 'Unspecified', linkedAccount: null };
  const doctor: StaffAccountCandidate = { userId: 'account-a', firstName: 'Actual', lastName: 'Account', email: 'account@example.invalid',
    roles: ['Doctor'], isActive: true, isLinkedToStaff: false };
  beforeEach(() => {
    vi.clearAllMocks(); admin = true; lookup = new Subject(); link = new Subject(); unlink = new Subject();
    vi.mocked(Swal.fire).mockResolvedValue({ isConfirmed: true } as any);
    service = { getStaff: vi.fn(() => of([staff])), lookupAccounts: vi.fn(() => lookup), linkAccount: vi.fn(() => link), unlinkAccount: vi.fn(() => unlink) };
    TestBed.configureTestingModule({ providers: [ { provide: StaffService, useValue: service }, { provide: AuthService, useValue: { isAdmin: () => admin } } ] });
    component = TestBed.createComponent(StaffList).componentInstance; component.staffMembers.set([staff]); component.openStaffDetailModal(staff);
  });
  afterEach(() => component.ngOnDestroy());
  function choose(account = doctor): void {
    component.openAccountDialog(); lookup.next({ items: [account], page: 1, pageSize: 20, hasMore: false }); component.selectAccount(account);
  }
  it('denies non-Admin lookup and mutation controls', async () => {
    admin = false; component.openAccountDialog(); component.selectedAccount.set(doctor); await component.submitAccountLink();
    component.selectedStaffForDetail.set({ ...staff, linkedAccount: doctor }); await component.unlinkAccount();
    expect(service.lookupAccounts).not.toHaveBeenCalled(); expect(service.linkAccount).not.toHaveBeenCalled(); expect(service.unlinkAccount).not.toHaveBeenCalled();
  });
  it('loads paged real accounts with explicit selection and empty/loading states', () => {
    component.openAccountDialog(); expect(component.accountsLoading()).toBe(true); expect(component.selectedAccount()).toBeNull();
    lookup.next({ items: [], page: 1, pageSize: 20, hasMore: false }); expect(component.accounts()).toEqual([]); expect(component.accountsLoading()).toBe(false);
    expect(service.lookupAccounts).toHaveBeenCalledWith('', 1); expect(component.canSubmitAccountLink).toBe(false);
  });
  it('rejects inactive and already-linked candidates', () => {
    choose(); component.selectedAccount.set(null); component.selectAccount({ ...doctor, isActive: false }); component.selectAccount({ ...doctor, isLinkedToStaff: true });
    expect(component.selectedAccount()).toBeNull(); expect(component.canSubmitAccountLink).toBe(false);
  });
  it('does not infer Doctor for another role and clearly allows only Unspecified', async () => {
    choose({ ...doctor, roles: ['Nurse'] }); expect(component.selectedStaffKind()).toBe(StaffKind.Unspecified);
    component.selectedStaffKind.set(StaffKind.Doctor); await component.submitAccountLink(); expect(service.linkAccount).not.toHaveBeenCalled();
  });
  it('blocks duplicate and cross-action requests from confirmation through completion', async () => {
    choose(); const first = component.submitAccountLink(); const second = component.submitAccountLink(); await Promise.all([first, second]);
    component.openStaffDetailModal({ ...staff, id: 'other' }); component.closeAccountDialog(); component.searchAccounts(); component.onEscapeKey();
    expect(service.linkAccount).toHaveBeenCalledOnce(); expect(service.linkAccount).toHaveBeenCalledWith('staff-a', 'account-a', StaffKind.Doctor);
    expect(component.accountPending()).toBe(true); expect(component.showAccountDialog()).toBe(true); expect(component.selectedStaffForDetail()?.id).toBe('staff-a');
  });
  it('updates linkage only after the persisted success response', async () => {
    choose(); await component.submitAccountLink(); expect(component.selectedStaffForDetail()?.linkedAccount).toBeNull();
    link.next({ staffId: staff.id, staffKind: 'Doctor', linkedAccount: doctor });
    expect(component.selectedStaffForDetail()?.linkedAccount?.userId).toBe('account-a'); expect(component.staffMembers()[0].staffKind).toBe('Doctor');
    expect(component.accountPending()).toBe(false); expect(component.showAccountDialog()).toBe(false);
  });
  it('cancellation performs no mutation', async () => {
    choose(); vi.mocked(Swal.fire).mockResolvedValueOnce({ isConfirmed: false } as any); await component.submitAccountLink();
    expect(service.linkAccount).not.toHaveBeenCalled(); expect(component.accountPending()).toBe(false);
  });
  it('reads safe ProblemDetails and refreshes without retrying the link', async () => {
    choose(); await component.submitAccountLink(); link.error({ status: 409, error: { detail: 'Account is already linked to another staff record.' } });
    expect(component.accountError()).toBe('Account is already linked to another staff record.'); expect(service.linkAccount).toHaveBeenCalledOnce();
    expect(service.getStaff).toHaveBeenCalledOnce(); expect(component.accountPending()).toBe(false);
  });
  it('confirms unlink and resets displayed classification after success without deleting Staff', async () => {
    component.selectedStaffForDetail.set({ ...staff, staffKind: 'Doctor', linkedAccount: doctor });
    component.staffMembers.set([component.selectedStaffForDetail()!]);
    await Promise.all([component.unlinkAccount(), component.unlinkAccount()]); expect(service.unlinkAccount).toHaveBeenCalledOnce();
    component.closeStaffDetails(); expect(component.showDetailModal()).toBe(true);
    unlink.next(); expect(component.selectedStaffForDetail()?.linkedAccount).toBeNull(); expect(component.staffMembers()[0].staffKind).toBe('Unspecified');
    expect(component.staffMembers()).toHaveLength(1); expect(component.accountPending()).toBe(false);
  });
  it('reads unlink failure detail without optimistic unlink or automatic retry', async () => {
    const linked = { ...staff, staffKind: 'Doctor', linkedAccount: doctor }; component.selectedStaffForDetail.set(linked);
    service.getStaff.mockReturnValue(of([linked])); await component.unlinkAccount(); unlink.error({ error: { detail: 'Unable to confirm the account change.' } });
    expect(component.accountError()).toBe('Unable to confirm the account change.'); expect(component.selectedStaffForDetail()?.linkedAccount).toEqual(doctor);
    expect(service.unlinkAccount).toHaveBeenCalledOnce();
  });
  it('ignores stale search results when a new search is started', () => {
    const later = new Subject<any>(); component.openAccountDialog(); service.lookupAccounts.mockReturnValue(later);
    component.accountSearch.set('another'); component.searchAccounts(); lookup.next({ items: [doctor], page: 1, hasMore: false });
    expect(component.accounts()).toEqual([]); later.next({ items: [], page: 1, hasMore: false }); expect(component.accountsLoading()).toBe(false);
  });
  it('displays lookup failure and prevents empty selection submission', () => {
    component.openAccountDialog(); lookup.error({ error: { detail: 'Lookup unavailable.' } });
    expect(component.accountError()).toBe('Lookup unavailable.'); expect(component.accountsLoading()).toBe(false); expect(component.canSubmitAccountLink).toBe(false);
  });
});
