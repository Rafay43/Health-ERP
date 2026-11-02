// StaffListComponent — Health ERP Doctors & Staff Directory Module
import { Component, inject, OnInit, OnDestroy, signal, computed, HostListener } from '@angular/core';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { StaffService } from './data-access/staff.service';
import { Staff, CreateStaffRequest, StaffAccountCandidate, StaffAccountLink, StaffKind } from './data-access/staff.model';
import { AuthService } from '../../core/services/auth.service';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-staff-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './staff-list.html',
})
export class StaffList implements OnInit, OnDestroy {
  private staffService = inject(StaffService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);

  get canAddStaff(): boolean {
    return this.authService.isAdmin();
  }

  readonly StaffKind = StaffKind;
  showAccountDialog = signal(false);
  accountPending = signal(false);
  accountsLoading = signal(false);
  accountError = signal<string | null>(null);
  accountSearch = signal('');
  accounts = signal<StaffAccountCandidate[]>([]);
  accountPage = signal(1);
  accountsHasMore = signal(false);
  selectedAccount = signal<StaffAccountCandidate | null>(null);
  selectedStaffKind = signal<StaffKind>(StaffKind.Doctor);
  private accountSearchRequest?: Subscription;

  get canSubmitAccountLink(): boolean {
    const staff = this.selectedStaffForDetail(); const account = this.selectedAccount(); const kind = this.selectedStaffKind();
    return this.canAddStaff && !!staff?.isActive && !staff.linkedAccount && !!account?.isActive && !account.isLinkedToStaff
      && (kind === StaffKind.Unspecified || (kind === StaffKind.Doctor && account.roles.includes('Doctor')))
      && !this.accountPending() && !this.accountsLoading();
  }

  openAccountDialog(): void {
    if (!this.canAddStaff || this.accountPending() || this.submitting() || !this.selectedStaffForDetail()?.isActive || this.selectedStaffForDetail()?.linkedAccount) return;
    this.accountError.set(null); this.accountSearch.set(''); this.selectedAccount.set(null);
    this.selectedStaffKind.set(StaffKind.Doctor); this.showDetailModal.set(false); this.showAccountDialog.set(true);
    document.body.style.overflow = 'hidden'; this.searchAccounts();
  }

  closeAccountDialog(): void {
    if (this.accountPending()) return;
    this.accountSearchRequest?.unsubscribe(); this.accountsLoading.set(false);
    this.showAccountDialog.set(false); this.showDetailModal.set(true);
  }

  searchAccounts(page = 1): void {
    if (!this.canAddStaff || !this.showAccountDialog() || this.accountPending()) return;
    this.accountSearchRequest?.unsubscribe(); this.accountsLoading.set(true); this.accountError.set(null); this.selectedAccount.set(null);
    this.accountSearchRequest = this.staffService.lookupAccounts(this.accountSearch().trim(), page).subscribe({
      next: result => { this.accounts.set(result.items); this.accountPage.set(result.page); this.accountsHasMore.set(result.hasMore); this.accountsLoading.set(false); },
      error: err => { this.accounts.set([]); this.accountsLoading.set(false); this.accountError.set(err.error?.detail || 'Unable to load accounts.'); }
    });
  }

  selectAccount(account: StaffAccountCandidate): void {
    if (this.accountPending() || this.accountsLoading() || !account.isActive || account.isLinkedToStaff) return;
    this.selectedAccount.set(account);
    if (!account.roles.includes('Doctor')) this.selectedStaffKind.set(StaffKind.Unspecified);
  }

  async submitAccountLink(): Promise<void> {
    if (!this.canSubmitAccountLink) return;
    const staff = this.selectedStaffForDetail()!; const account = this.selectedAccount()!; const kind = this.selectedStaffKind();
    this.accountPending.set(true); this.accountError.set(null);
    try {
      const confirmation = await Swal.fire({ title: 'Link this account?', text: `${account.firstName} ${account.lastName} (${account.email || 'No email'}). Existing account sessions will be signed out.`,
        icon: 'warning', showCancelButton: true, confirmButtonText: 'Link Account', confirmButtonColor: '#0F766E' });
      if (!confirmation.isConfirmed) { this.accountPending.set(false); return; }
      this.staffService.linkAccount(staff.id, account.userId, kind).subscribe({
        next: result => { this.applyAccountState(result); this.accountPending.set(false); this.closeAccountDialog();
          Swal.fire({ title: 'Account Linked', text: 'Association saved. Existing account sessions were invalidated.', icon: 'success', timer: 2200 }); },
        error: err => this.accountFailure(err)
      });
    } catch { this.accountPending.set(false); this.accountError.set('Unable to confirm the account link.'); }
  }

  async unlinkAccount(): Promise<void> {
    const staff = this.selectedStaffForDetail();
    if (!this.canAddStaff || this.accountPending() || this.submitting() || !staff?.linkedAccount) return;
    this.accountPending.set(true); this.accountError.set(null);
    try {
      const confirmation = await Swal.fire({ title: 'Unlink this account?', text: 'The association will be removed, classification reset to Unspecified, and existing account sessions signed out. Staff and account records will be kept.',
        icon: 'warning', showCancelButton: true, confirmButtonText: 'Unlink Account', confirmButtonColor: '#0F766E' });
      if (!confirmation.isConfirmed) { this.accountPending.set(false); return; }
      this.staffService.unlinkAccount(staff.id).subscribe({
        next: () => { this.applyAccountState({ staffId: staff.id, staffKind: 'Unspecified', linkedAccount: null }); this.accountPending.set(false);
          Swal.fire({ title: 'Account Unlinked', text: 'Association removed. Existing account sessions were invalidated.', icon: 'success', timer: 2200 }); },
        error: err => this.accountFailure(err)
      });
    } catch { this.accountPending.set(false); this.accountError.set('Unable to confirm account unlinking.'); }
  }

  private applyAccountState(result: StaffAccountLink): void {
    const update = (staff: Staff) => staff.id === result.staffId ? { ...staff, staffKind: result.staffKind, linkedAccount: result.linkedAccount } : staff;
    this.staffMembers.update(rows => rows.map(update));
    if (this.selectedStaffForDetail()) this.selectedStaffForDetail.update(staff => staff ? update(staff) : null);
  }

  private accountFailure(err: any): void {
    this.accountPending.set(false);
    this.accountError.set(err.error?.detail || err.error?.message || 'Unable to confirm the account change. Refresh staff and accounts before retrying.');
    this.loadStaff(); // Refresh persisted state, never retry the mutation.
  }

  closeStaffDetails(): void {
    if (this.accountPending()) return;
    this.showDetailModal.set(false); document.body.style.overflow = '';
  }

  ngOnDestroy(): void { this.accountSearchRequest?.unsubscribe(); document.body.style.overflow = ''; }

  // Global Math for template calculations
  Math = Math;

  // Data Signals
  staffMembers = signal<Staff[]>([]);
  loading = signal<boolean>(true);
  submitting = signal<boolean>(false);

  // Filters & Search
  searchQuery = signal<string>('');
  selectedDepartment = signal<string>('');
  selectedSpecialty = signal<string>('');
  selectedStatus = signal<string>('');

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Modal State Signals
  showAddStaffModal = signal<boolean>(false);
  showDetailModal = signal<boolean>(false);
  selectedStaffForDetail = signal<Staff | null>(null);

  // Error Feedback
  createError = signal<string | null>(null);

  // Reactive Form
  staffForm = this.fb.group({
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: [''],
    specialty: ['', [Validators.required]],
    licenseNumber: [''],
    department: ['', [Validators.required]],
  });

  // Computed KPI Signals
  totalStaffCount = computed(() => this.staffMembers().length);

  activeStaffCount = computed(() =>
    this.staffMembers().filter((s) => s.isActive).length
  );

  uniqueDepartments = computed(() => {
    const deps = this.staffMembers().map((s) => s.department).filter(Boolean);
    return Array.from(new Set(deps)).sort();
  });

  uniqueSpecialties = computed(() => {
    const specs = this.staffMembers().map((s) => s.specialty).filter(Boolean);
    return Array.from(new Set(specs)).sort();
  });

  departmentsCount = computed(() => this.uniqueDepartments().length);
  specialtiesCount = computed(() => this.uniqueSpecialties().length);

  // Filtered & Paginated List
  filteredStaff = computed(() => {
    let list = this.staffMembers();

    // 1. Search Query (Name, Specialty, Email, License)
    const query = this.searchQuery().trim().toLowerCase();
    if (query) {
      list = list.filter(
        (s) =>
          `${s.firstName} ${s.lastName}`.toLowerCase().includes(query) ||
          s.specialty?.toLowerCase().includes(query) ||
          s.email?.toLowerCase().includes(query) ||
          s.licenseNumber?.toLowerCase().includes(query) ||
          s.department?.toLowerCase().includes(query)
      );
    }

    // 2. Department Filter
    const dep = this.selectedDepartment();
    if (dep) {
      list = list.filter((s) => s.department.toLowerCase() === dep.toLowerCase());
    }

    // 3. Specialty Filter
    const spec = this.selectedSpecialty();
    if (spec) {
      list = list.filter((s) => s.specialty.toLowerCase() === spec.toLowerCase());
    }

    // 4. Status Filter
    const status = this.selectedStatus();
    if (status === 'active') {
      list = list.filter((s) => s.isActive);
    } else if (status === 'inactive') {
      list = list.filter((s) => !s.isActive);
    }

    return list;
  });

  paginatedStaff = computed(() => {
    const list = this.filteredStaff();
    const page = this.currentPage();
    const size = this.pageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  totalPages = computed(() => {
    const count = this.filteredStaff().length;
    return Math.ceil(count / this.pageSize()) || 1;
  });

  ngOnInit(): void {
    this.loadStaff();
  }

  @HostListener('window:keydown.escape')
  onEscapeKey(): void {
    if (this.accountPending()) return;
    if (this.showAccountDialog()) { this.closeAccountDialog(); return; }
    if (this.showAddStaffModal()) this.showAddStaffModal.set(false);
    if (this.showDetailModal()) this.showDetailModal.set(false);
    document.body.style.overflow = '';
  }

  loadStaff(): void {
    this.loading.set(true);
    this.staffService.getStaff().subscribe({
      next: (data) => {
        this.staffMembers.set(data || []);
        const selectedId = this.selectedStaffForDetail()?.id;
        if (selectedId) this.selectedStaffForDetail.set(data.find(s => s.id === selectedId) || null);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onKpiClick(type: 'total' | 'active' | 'dept' | 'spec'): void {
    this.currentPage.set(1);
    if (type === 'active') {
      this.selectedStatus.set('active');
      this.searchQuery.set('');
    } else if (type === 'total') {
      this.selectedStatus.set('');
      this.selectedDepartment.set('');
      this.selectedSpecialty.set('');
      this.searchQuery.set('');
    } else if (type === 'dept') {
      this.selectedStatus.set('');
      this.searchQuery.set('');
    } else if (type === 'spec') {
      this.selectedStatus.set('');
      this.searchQuery.set('');
    }
  }

  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedDepartment.set('');
    this.selectedSpecialty.set('');
    this.selectedStatus.set('');
    this.currentPage.set(1);
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  onBackdropClick(event: MouseEvent, modalType: 'add' | 'detail'): void {
    if (this.accountPending()) return;
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      if (modalType === 'add') this.showAddStaffModal.set(false);
      if (modalType === 'detail') this.showDetailModal.set(false);
      document.body.style.overflow = '';
    }
  }

  // Modal 1: Add Staff Member
  openAddStaffModal(): void {
    if (this.accountPending()) return;
    this.createError.set(null);
    this.staffForm.reset({
      firstName: '',
      lastName: '',
      email: '',
      phoneNumber: '',
      specialty: '',
      licenseNumber: '',
      department: '',
    });
    this.showAddStaffModal.set(true);
    document.body.style.overflow = 'hidden';
  }

  submitAddStaff(): void {
    if (this.staffForm.invalid) {
      this.staffForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.createError.set(null);

    const val = this.staffForm.value;
    const req: CreateStaffRequest = {
      firstName: val.firstName!.trim(),
      lastName: val.lastName!.trim(),
      email: val.email!.trim(),
      phoneNumber: val.phoneNumber?.trim() || '',
      specialty: val.specialty!.trim(),
      licenseNumber: val.licenseNumber?.trim() || '',
      department: val.department!.trim(),
    };

    this.staffService.createStaff(req).subscribe({
      next: () => {
        this.submitting.set(false);
        this.showAddStaffModal.set(false);
        document.body.style.overflow = '';
        this.loadStaff();

        Swal.fire({
          title: 'Staff Member Added',
          text: `Dr. / Staff ${req.firstName} ${req.lastName} has been added to the directory.`,
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2200,
          timerProgressBar: true,
        });
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err.error?.detail || err.error?.message || 'Failed to add staff member. Please check the entered information.';
        this.createError.set(msg);
      },
    });
  }

  // Modal 2: View Details
  openStaffDetailModal(staff: Staff): void {
    if (this.accountPending()) return;
    this.accountError.set(null);
    this.selectedStaffForDetail.set(staff);
    this.showDetailModal.set(true);
    document.body.style.overflow = 'hidden';
  }

  // Helpers
  getInitials(firstName: string, lastName: string): string {
    const f = firstName ? firstName.charAt(0).toUpperCase() : '';
    const l = lastName ? lastName.charAt(0).toUpperCase() : '';
    return `${f}${l}` || 'ST';
  }

  getAvatarColorClass(firstName: string): string {
    const colors = [
      'bg-[#0F766E]/10 text-[#0F766E] border-[#0F766E]/20',
      'bg-blue-50 text-blue-700 border-blue-200',
      'bg-violet-50 text-violet-700 border-violet-200',
      'bg-amber-50 text-amber-700 border-amber-200',
      'bg-emerald-50 text-emerald-700 border-emerald-200',
      'bg-indigo-50 text-indigo-700 border-indigo-200',
    ];
    let hash = 0;
    for (let i = 0; i < firstName.length; i++) {
      hash = firstName.charCodeAt(i) + ((hash << 5) - hash);
    }
    const index = Math.abs(hash) % colors.length;
    return colors[index];
  }

  formatPhoneDisplay(phone: string): string {
    if (!phone) return '-';
    // Format visual spacing for raw numbers if unformatted e.g. +923006190788 -> +92 300 6190788
    if (phone.startsWith('+92') && phone.length === 13 && !phone.includes(' ')) {
      return `${phone.substring(0, 3)} ${phone.substring(3, 6)} ${phone.substring(6)}`;
    }
    return phone;
  }
}
