import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import Swal from 'sweetalert2';
import { BillingList } from './billing-list';
import { BillingService } from './data-access/billing.service';
import { Invoice } from './data-access/billing.model';
import { PatientService } from '../patients/data-access/patient.service';

vi.mock('sweetalert2', () => ({ default: { fire: vi.fn(() => Promise.resolve({})) } }));

describe('Billing payment integrity UI', () => {
  let component: BillingList;
  let request: Subject<void>;
  let service: { recordPayment: ReturnType<typeof vi.fn>; getInvoices: ReturnType<typeof vi.fn> };
  const pending: Invoice = { id: 'invoice-a', patientId: 'patient-a', patientName: 'Test Patient',
    invoiceNumber: 'INV-A', issueDateUtc: '2026-10-12T00:00:00Z', dueDateUtc: '2026-11-12T00:00:00Z',
    status: 'Pending', totalAmount: 100, paidAmount: 0, balanceDue: 100, lineItems: [], payments: [] };

  beforeEach(() => {
    vi.clearAllMocks(); request = new Subject<void>();
    service = { recordPayment: vi.fn(() => request), getInvoices: vi.fn(() => of([pending])) };
    TestBed.configureTestingModule({ providers: [
      { provide: BillingService, useValue: service },
      { provide: PatientService, useValue: { getPatients: () => of([]) } },
    ] });
    component = TestBed.createComponent(BillingList).componentInstance;
    component.invoices.set([pending]); component.openPaymentDialog(pending);
    component.paymentForm.patchValue({ amount: 30 });
  });

  afterEach(() => { document.body.style.overflow = ''; });

  it('does not optimistically mutate finances and blocks repeated pending submissions', () => {
    component.submitPayment(); component.submitPayment();
    expect(service.recordPayment).toHaveBeenCalledOnce();
    expect(component.submittingPayment()).toBe(true);
    expect(component.invoices()[0]).toEqual(pending);
    expect(Swal.fire).not.toHaveBeenCalled();
    expect(service.recordPayment).toHaveBeenCalledWith('invoice-a', { amount: 30, paymentMethod: 1, referenceNumber: '', notes: '' });
  });

  it('refreshes paid amount, balance, status and both modal snapshots from persisted history after success', () => {
    const refreshed: Invoice = { ...pending, paidAmount: 30, balanceDue: 70, status: 'PartiallyPaid',
      payments: [{ id: 'payment-a', amount: 30, paymentMethod: 'Cash', paymentDateUtc: '2026-10-12T01:00:00Z' }] };
    component.selectedInvoiceForDetail.set(pending);
    service.getInvoices.mockReturnValue(of([refreshed]));
    component.submitPayment(); request.next(); request.complete();
    expect(component.invoices()[0]).toEqual(refreshed);
    expect(component.selectedInvoiceForDetail()).toEqual(refreshed);
    expect(component.selectedInvoiceForPayment()).toEqual(refreshed);
    expect(component.showPaymentModal()).toBe(false);
    expect(component.submittingPayment()).toBe(false);
    expect(Swal.fire).toHaveBeenCalledWith(expect.objectContaining({ icon: 'success' }));
  });

  it('shows the conflict and refreshes without claiming payment success', () => {
    const refreshed: Invoice = { ...pending, paidAmount: 95, balanceDue: 5, status: 'PartiallyPaid' };
    service.getInvoices.mockReturnValue(of([refreshed]));
    component.submitPayment(); request.error({ status: 409, error: { detail: 'Payment amount exceeds balance due.' } });
    expect(component.paymentError()).toBe('Payment amount exceeds balance due.');
    expect(component.selectedInvoiceForPayment()?.balanceDue).toBe(5);
    expect(component.invoices()[0].paidAmount).toBe(95);
    expect(component.showPaymentModal()).toBe(true);
    expect(component.submittingPayment()).toBe(false);
    expect(Swal.fire).not.toHaveBeenCalled();
  });

  it('refreshes on an uncertain storage or network failure and does not automatically retry', () => {
    component.submitPayment(); request.error({ status: 500, error: { detail: 'Unable to confirm payment. Refresh before retrying.' } });
    expect(component.paymentError()).toContain('Unable to confirm payment');
    expect(service.getInvoices).toHaveBeenCalledOnce();
    expect(service.recordPayment).toHaveBeenCalledOnce();
    expect(component.invoices()[0]).toEqual(pending);
    expect(Swal.fire).not.toHaveBeenCalled();
  });

  it('allows a deliberate corrected retry after rejection', () => {
    component.submitPayment(); request.error({ status: 400, error: { detail: 'Invalid amount.' } });
    service.recordPayment.mockReturnValue(new Subject<void>());
    component.paymentForm.patchValue({ amount: 20 }); component.submitPayment();
    expect(service.recordPayment).toHaveBeenCalledTimes(2);
    expect(component.paymentError()).toBeNull();
  });
});
