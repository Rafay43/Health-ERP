// BillingListComponent — Health ERP Financial Operations Module
import { Component, inject, OnInit, signal, computed, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';

import { BillingService } from './data-access/billing.service';
import { Invoice, RecordPaymentRequest, CreateInvoiceRequest } from './data-access/billing.model';
import { PatientService } from '../patients/data-access/patient.service';
import { Patient } from '../patients/data-access/patient.model';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-billing-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './billing-list.html',
})
export class BillingList implements OnInit {
  private billingService = inject(BillingService);
  private patientService = inject(PatientService);
  private fb = inject(FormBuilder);

  // Global Math for template calculations
  Math = Math;

  // Data Signals
  invoices = signal<Invoice[]>([]);
  patientList = signal<Patient[]>([]);
  loading = signal<boolean>(true);
  submittingPayment = signal<boolean>(false);
  submittingInvoice = signal<boolean>(false);

  // Tab & Filters
  activeTab = signal<'invoices' | 'claims'>('invoices');
  searchQuery = signal<string>('');
  selectedStatus = signal<string>('');

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Row Expansion State (Invoice ID)
  expandedInvoiceId = signal<string | null>(null);

  // Modal Visibility Signals
  showPaymentModal = signal<boolean>(false);
  showCreateInvoiceModal = signal<boolean>(false);
  showDetailModal = signal<boolean>(false);

  selectedInvoiceForPayment = signal<Invoice | null>(null);
  selectedInvoiceForDetail = signal<Invoice | null>(null);

  // Feedback Signals
  paymentError = signal<string | null>(null);
  createInvoiceError = signal<string | null>(null);

  // Reactive Forms
  paymentForm = this.fb.group({
    amount: [0, [Validators.required, Validators.min(0.01)]],
    paymentMethod: [1, [Validators.required]],
    referenceNumber: [''],
    notes: [''],
  });

  createInvoiceForm = this.fb.group({
    patientId: ['', Validators.required],
    dueDateUtc: [this.getDefaultDueDate(), Validators.required],
    lineItems: this.fb.array([]),
  });

  // Computed KPI Signals
  totalCollected = computed(() =>
    this.invoices().reduce((acc, curr) => acc + (curr.paidAmount || 0), 0)
  );

  totalBalanceDue = computed(() =>
    this.invoices().reduce((acc, curr) => acc + (curr.balanceDue || 0), 0)
  );

  totalInvoicesCount = computed(() => this.invoices().length);

  pendingCount = computed(() =>
    this.invoices().filter((i) => i.status === 'Pending' || i.status === 'PartiallyPaid').length
  );

  paidInvoicesCount = computed(() =>
    this.invoices().filter((i) => i.status === 'Paid').length
  );

  // Filtered & Paginated List
  filteredInvoices = computed(() => {
    let list = this.invoices();

    // 1. Search Query (Invoice # or Patient Name)
    const query = this.searchQuery().trim().toLowerCase();
    if (query) {
      list = list.filter(
        (i) =>
          i.invoiceNumber.toLowerCase().includes(query) ||
          i.patientName.toLowerCase().includes(query)
      );
    }

    // 2. Status Filter
    const status = this.selectedStatus();
    if (status) {
      list = list.filter((i) => i.status.toLowerCase() === status.toLowerCase());
    }

    // Sort newest invoices first
    return [...list].sort((a, b) => new Date(b.issueDateUtc).getTime() - new Date(a.issueDateUtc).getTime());
  });

  paginatedInvoices = computed(() => {
    const list = this.filteredInvoices();
    const page = this.currentPage();
    const size = this.pageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  totalPages = computed(() => {
    const count = this.filteredInvoices().length;
    return Math.ceil(count / this.pageSize()) || 1;
  });

  get lineItemsArray(): FormArray {
    return this.createInvoiceForm.get('lineItems') as FormArray;
  }

  get calculatedInvoiceTotal(): number {
    return this.lineItemsArray.controls.reduce((acc, ctrl) => {
      const q = Number(ctrl.get('quantity')?.value) || 0;
      const p = Number(ctrl.get('unitPrice')?.value) || 0;
      return acc + q * p;
    }, 0);
  }

  ngOnInit(): void {
    this.loadInvoices();
    this.loadPatients();
  }

  @HostListener('window:keydown.escape')
  onEscapeKey(): void {
    if (this.showPaymentModal()) this.showPaymentModal.set(false);
    if (this.showCreateInvoiceModal()) this.showCreateInvoiceModal.set(false);
    if (this.showDetailModal()) this.showDetailModal.set(false);
    document.body.style.overflow = '';
  }

  loadInvoices(): void {
    this.loading.set(true);
    this.billingService.getInvoices().subscribe({
      next: (data) => {
        this.invoices.set(data || []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  loadPatients(): void {
    this.patientService.getPatients().subscribe({
      next: (patients) => this.patientList.set(patients || []),
    });
  }

  toggleTab(tab: 'invoices' | 'claims'): void {
    this.activeTab.set(tab);
  }

  toggleRowExpansion(invoiceId: string): void {
    if (this.expandedInvoiceId() === invoiceId) {
      this.expandedInvoiceId.set(null);
    } else {
      this.expandedInvoiceId.set(invoiceId);
    }
  }

  onKpiClick(type: 'collected' | 'balance' | 'total' | 'pending'): void {
    this.currentPage.set(1);
    if (type === 'collected') {
      this.selectedStatus.set('Paid');
      this.searchQuery.set('');
    } else if (type === 'balance') {
      this.selectedStatus.set('Pending');
      this.searchQuery.set('');
    } else if (type === 'total') {
      this.selectedStatus.set('');
      this.searchQuery.set('');
    } else if (type === 'pending') {
      this.selectedStatus.set('Pending');
      this.searchQuery.set('');
    }
  }

  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedStatus.set('');
    this.currentPage.set(1);
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  onBackdropClick(event: MouseEvent, dialogType: 'payment' | 'createInvoice' | 'detail'): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      if (dialogType === 'payment') this.showPaymentModal.set(false);
      if (dialogType === 'createInvoice') this.showCreateInvoiceModal.set(false);
      if (dialogType === 'detail') this.showDetailModal.set(false);
      document.body.style.overflow = '';
    }
  }

  // Payment Modal
  openPaymentDialog(invoice: Invoice): void {
    this.selectedInvoiceForPayment.set(invoice);
    this.paymentError.set(null);
    this.paymentForm.reset({
      amount: invoice.balanceDue,
      paymentMethod: 1,
      referenceNumber: '',
      notes: '',
    });
    this.showPaymentModal.set(true);
    document.body.style.overflow = 'hidden';
  }

  submitPayment(): void {
    const inv = this.selectedInvoiceForPayment();
    if (!inv || this.paymentForm.invalid) return;

    this.submittingPayment.set(true);
    this.paymentError.set(null);

    const val = this.paymentForm.value;
    const req: RecordPaymentRequest = {
      amount: Number(val.amount),
      paymentMethod: Number(val.paymentMethod),
      referenceNumber: val.referenceNumber?.trim() || '',
      notes: val.notes?.trim() || '',
    };

    this.billingService.recordPayment(inv.id, req).subscribe({
      next: () => {
        this.submittingPayment.set(false);
        this.showPaymentModal.set(false);
        document.body.style.overflow = '';
        this.loadInvoices();

        Swal.fire({
          title: 'Payment Recorded',
          text: `Payment receipt of $${req.amount.toFixed(2)} applied successfully to ${inv.invoiceNumber}.`,
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2500,
          timerProgressBar: true,
        });
      },
      error: (err) => {
        this.submittingPayment.set(false);
        const msg = err.error?.detail || err.error?.message || 'Failed to record payment receipt.';
        this.paymentError.set(msg);
      },
    });
  }

  // Detail Modal
  openInvoiceDetailModal(invoice: Invoice): void {
    this.selectedInvoiceForDetail.set(invoice);
    this.showDetailModal.set(true);
    document.body.style.overflow = 'hidden';
  }

  // Create Invoice Modal
  openCreateInvoiceModal(): void {
    this.createInvoiceError.set(null);
    const firstPatient = this.patientList().length > 0 ? this.patientList()[0].id : '';

    this.createInvoiceForm.reset({
      patientId: firstPatient,
      dueDateUtc: this.getDefaultDueDate(),
    });

    this.lineItemsArray.clear();
    this.addLineItem('Medical Consultation', 1, 150);

    this.showCreateInvoiceModal.set(true);
    document.body.style.overflow = 'hidden';
  }

  addLineItem(desc: string = '', qty: number = 1, price: number = 0): void {
    const group = this.fb.group({
      description: [desc, Validators.required],
      quantity: [qty, [Validators.required, Validators.min(1)]],
      unitPrice: [price, [Validators.required, Validators.min(0)]],
    });
    this.lineItemsArray.push(group);
  }

  removeLineItem(index: number): void {
    if (this.lineItemsArray.length > 1) {
      this.lineItemsArray.removeAt(index);
    }
  }

  submitCreateInvoice(): void {
    if (this.createInvoiceForm.invalid) return;

    this.submittingInvoice.set(true);
    this.createInvoiceError.set(null);

    const val = this.createInvoiceForm.value;
    const req: CreateInvoiceRequest = {
      patientId: val.patientId!,
      dueDateUtc: new Date(val.dueDateUtc!).toISOString(),
      lineItems: this.lineItemsArray.controls.map((ctrl) => ({
        description: ctrl.get('description')?.value.trim(),
        quantity: Number(ctrl.get('quantity')?.value),
        unitPrice: Number(ctrl.get('unitPrice')?.value),
      })),
    };

    this.billingService.createInvoice(req).subscribe({
      next: () => {
        this.submittingInvoice.set(false);
        this.showCreateInvoiceModal.set(false);
        document.body.style.overflow = '';
        this.loadInvoices();

        Swal.fire({
          title: 'Invoice Created',
          text: 'Patient billing invoice has been generated successfully.',
          icon: 'success',
          confirmButtonColor: '#0F766E',
          timer: 2500,
          timerProgressBar: true,
        });
      },
      error: (err) => {
        this.submittingInvoice.set(false);
        const msg = err.error?.detail || err.error?.message || 'Failed to create invoice.';
        this.createInvoiceError.set(msg);
      },
    });
  }

  // Helpers
  getDefaultDueDate(): string {
    const date = new Date();
    date.setDate(date.getDate() + 30);
    return date.toISOString().split('T')[0];
  }

  getPatientMrn(patientId: string): string {
    const patient = this.patientList().find((p) => p.id === patientId);
    return patient ? patient.medicalRecordNumber : '';
  }

  getStatusBadgeClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'paid':
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold';
      case 'partiallypaid':
        return 'bg-blue-50 text-blue-700 border border-blue-200 font-semibold';
      case 'pending':
        return 'bg-amber-50 text-amber-700 border border-amber-200 font-semibold';
      case 'cancelled':
        return 'bg-rose-50 text-rose-700 border border-rose-200 font-semibold';
      default:
        return 'bg-slate-100 text-slate-700 border border-slate-200 font-medium';
    }
  }

  isOverdue(inv: Invoice): boolean {
    if (!inv || inv.balanceDue <= 0 || inv.status === 'Paid' || inv.status === 'Cancelled') {
      return false;
    }
    const due = new Date(inv.dueDateUtc).getTime();
    const now = new Date().getTime();
    return due < now;
  }

  formatDate(isoString: string): string {
    if (!isoString) return '-';
    const d = new Date(isoString);
    if (isNaN(d.getTime())) return isoString;
    return d.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  }

  getPaymentMethodName(method: number | string): string {
    const m = Number(method);
    switch (m) {
      case 1:
        return 'Cash';
      case 2:
        return 'Credit Card';
      case 3:
        return 'Insurance Claim';
      case 4:
        return 'Bank Transfer';
      default:
        return String(method);
    }
  }
}
