import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';

import { BillingService } from './data-access/billing.service';
import { Invoice } from './data-access/billing.model';

@Component({
  selector: 'app-billing-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    TableModule,
    ButtonModule,
    TagModule,
    TooltipModule,
    InputTextModule,
    DialogModule,
    MessageModule,
  ],
  template: `
    <div class="flex flex-col gap-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Billing & Financial Claims</h1>
          <p class="text-sm text-[var(--color-text-secondary)]">Manage patient consultation invoices, payments, and account balances</p>
        </div>
      </div>

      <!-- Financial Metrics Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-emerald-100 text-emerald-600 rounded-lg">
            <i class="pi pi-dollar text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Total Collected</p>
            <h3 class="text-xl font-bold text-[var(--color-text-primary)]">$ {{ totalCollected() | number:'1.2-2' }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-amber-100 text-amber-600 rounded-lg">
            <i class="pi pi-exclamation-circle text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Balance Due</p>
            <h3 class="text-xl font-bold text-amber-600">$ {{ totalBalanceDue() | number:'1.2-2' }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-blue-100 text-blue-600 rounded-lg">
            <i class="pi pi-file text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Total Invoices</p>
            <h3 class="text-xl font-bold text-[var(--color-text-primary)]">{{ invoices().length }}</h3>
          </div>
        </div>

        <div class="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)] shadow-xs flex items-center gap-4">
          <div class="p-3 bg-rose-100 text-rose-600 rounded-lg">
            <i class="pi pi-clock text-xl"></i>
          </div>
          <div>
            <p class="text-xs font-medium text-[var(--color-text-secondary)] uppercase">Pending Claims</p>
            <h3 class="text-xl font-bold text-rose-600">{{ pendingCount() }}</h3>
          </div>
        </div>
      </div>

      <!-- Filters & Actions -->
      <div class="bg-[var(--color-surface)] p-4 rounded-lg border border-[var(--color-border)] flex flex-wrap items-center gap-4">
        <div class="flex-1 min-w-[200px]">
          <div class="relative">
            <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"></i>
            <input
              type="text"
              pInputText
              [(ngModel)]="searchQuery"
              placeholder="Search by invoice # or patient name..."
              class="w-full pl-9 text-sm"
            />
          </div>
        </div>

        <div class="flex items-center gap-2">
          <label class="text-xs font-semibold text-[var(--color-text-secondary)] uppercase">Status:</label>
          <select
            [(ngModel)]="selectedStatus"
            class="bg-[var(--color-bg)] border border-[var(--color-border)] text-[var(--color-text-primary)] rounded px-3 py-1.5 text-sm focus:outline-none focus:border-[var(--color-primary)]"
          >
            <option value="">All Invoices</option>
            <option value="Pending">Pending</option>
            <option value="PartiallyPaid">Partially Paid</option>
            <option value="Paid">Paid</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
      </div>

      <!-- Invoices Table -->
      <div class="bg-[var(--color-surface)] rounded-lg border border-[var(--color-border)] p-4">
        <p-table [value]="filteredInvoices()" [loading]="isLoading()" [paginator]="true" [rows]="10" dataKey="id">
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 3rem"></th>
              <th>Invoice #</th>
              <th>Patient</th>
              <th>Issue Date</th>
              <th>Due Date</th>
              <th>Total</th>
              <th>Paid</th>
              <th>Balance</th>
              <th>Status</th>
              <th class="text-center">Action</th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-inv let-expanded="expanded">
            <tr>
              <td>
                <p-button
                  type="button"
                  [pRowToggler]="inv"
                  [text]="true"
                  [rounded]="true"
                  [icon]="expanded ? 'pi pi-chevron-down' : 'pi pi-chevron-right'"
                ></p-button>
              </td>
              <td class="font-mono font-bold text-[var(--color-primary)]">{{ inv.invoiceNumber }}</td>
              <td class="font-semibold text-[var(--color-text-primary)]">{{ inv.patientName }}</td>
              <td>{{ inv.issueDateUtc | date:'mediumDate' }}</td>
              <td>{{ inv.dueDateUtc | date:'mediumDate' }}</td>
              <td class="font-semibold">$ {{ inv.totalAmount | number:'1.2-2' }}</td>
              <td class="text-emerald-600 font-medium">$ {{ inv.paidAmount | number:'1.2-2' }}</td>
              <td class="font-bold" [class.text-rose-600]="inv.balanceDue > 0" [class.text-slate-400]="inv.balanceDue === 0">
                $ {{ inv.balanceDue | number:'1.2-2' }}
              </td>
              <td>
                <p-tag [value]="inv.status" [severity]="getStatusSeverity(inv.status)"></p-tag>
              </td>
              <td class="text-center">
                @if (inv.balanceDue > 0 && inv.status !== 'Cancelled') {
                  <p-button
                    label="Record Payment"
                    icon="pi pi-credit-card"
                    severity="success"
                    size="small"
                    (onClick)="openPaymentDialog(inv)"
                  ></p-button>
                } @else {
                  <span class="text-xs text-slate-400 font-medium">Settled</span>
                }
              </td>
            </tr>
          </ng-template>

          <!-- Expanded Row: Line Items & Payment History -->
          <ng-template pTemplate="rowexpansion" let-inv>
            <tr>
              <td colspan="10" class="p-4 bg-[var(--color-bg)]">
                <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <!-- Line Items -->
                  <div>
                    <h4 class="text-xs font-bold uppercase tracking-wider text-[var(--color-text-secondary)] mb-2 flex items-center gap-1">
                      <i class="pi pi-list"></i> Invoice Line Items
                    </h4>
                    <div class="border rounded-lg bg-[var(--color-surface)] overflow-hidden">
                      <table class="w-full text-xs text-left">
                        <thead class="bg-slate-50 border-b text-slate-600">
                          <tr>
                            <th class="p-2">Description</th>
                            <th class="p-2 text-center">Qty</th>
                            <th class="p-2 text-right">Price</th>
                            <th class="p-2 text-right">Total</th>
                          </tr>
                        </thead>
                        <tbody>
                          <tr *ngFor="let item of inv.lineItems" class="border-b last:border-0">
                            <td class="p-2 font-medium">{{ item.description }}</td>
                            <td class="p-2 text-center">{{ item.quantity }}</td>
                            <td class="p-2 text-right">$ {{ item.unitPrice | number:'1.2-2' }}</td>
                            <td class="p-2 text-right font-semibold">$ {{ item.lineTotal | number:'1.2-2' }}</td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                  </div>

                  <!-- Payment History -->
                  <div>
                    <h4 class="text-xs font-bold uppercase tracking-wider text-[var(--color-text-secondary)] mb-2 flex items-center gap-1">
                      <i class="pi pi-history"></i> Payment Receipts
                    </h4>
                    @if (inv.payments && inv.payments.length > 0) {
                      <div class="border rounded-lg bg-[var(--color-surface)] overflow-hidden">
                        <table class="w-full text-xs text-left">
                          <thead class="bg-slate-50 border-b text-slate-600">
                            <tr>
                              <th class="p-2">Date</th>
                              <th class="p-2">Method</th>
                              <th class="p-2">Ref #</th>
                              <th class="p-2 text-right">Amount</th>
                            </tr>
                          </thead>
                          <tbody>
                            <tr *ngFor="let pay of inv.payments" class="border-b last:border-0">
                              <td class="p-2">{{ pay.paymentDateUtc | date:'short' }}</td>
                              <td class="p-2 font-medium">{{ pay.paymentMethod }}</td>
                              <td class="p-2 text-slate-500">{{ pay.referenceNumber || '-' }}</td>
                              <td class="p-2 text-right font-bold text-emerald-600">$ {{ pay.amount | number:'1.2-2' }}</td>
                            </tr>
                          </tbody>
                        </table>
                      </div>
                    } @else {
                      <p class="text-xs text-slate-400 italic p-3 bg-[var(--color-surface)] border rounded-lg">
                        No payment receipts recorded yet for this invoice.
                      </p>
                    }
                  </div>
                </div>
              </td>
            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="10" class="text-center py-8 text-[var(--color-text-secondary)]">
                No invoices found matching criteria. Complete a consultation appointment to automatically generate an invoice.
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>

      <!-- Record Payment Modal Dialog -->
      <p-dialog
        header="Record Payment Receipt"
        [(visible)]="showPaymentDialog"
        [modal]="true"
        [style]="{ width: '450px' }"
      >
        @if (selectedInvoice()) {
          <div class="flex flex-col gap-4 py-2">
            <div class="bg-slate-50 p-3 rounded border text-sm flex justify-between items-center">
              <div>
                <p class="text-xs text-slate-500 font-semibold uppercase">Invoice</p>
                <p class="font-bold text-slate-800">{{ selectedInvoice()?.invoiceNumber }}</p>
              </div>
              <div class="text-right">
                <p class="text-xs text-slate-500 font-semibold uppercase">Balance Due</p>
                <p class="font-bold text-rose-600">$ {{ selectedInvoice()?.balanceDue | number:'1.2-2' }}</p>
              </div>
            </div>

            <form [formGroup]="paymentForm" (ngSubmit)="onSavePayment()" class="flex flex-col gap-3">
              <div *ngIf="paymentError()" class="mb-1">
                <p-message severity="error" [text]="paymentError()!"></p-message>
              </div>

              <div class="flex flex-col gap-1">
                <label class="text-xs font-semibold text-slate-700">Payment Amount (USD) *</label>
                <input
                  type="number"
                  step="0.01"
                  pInputText
                  formControlName="amount"
                  class="w-full"
                />
              </div>

              <div class="flex flex-col gap-1">
                <label class="text-xs font-semibold text-slate-700">Payment Method *</label>
                <select
                  formControlName="paymentMethod"
                  class="w-full bg-white border border-slate-300 text-slate-800 rounded p-2 text-sm focus:outline-none focus:border-emerald-500"
                >
                  <option [value]="1">Cash</option>
                  <option [value]="2">Credit Card</option>
                  <option [value]="3">Insurance Claim</option>
                  <option [value]="4">Bank Transfer</option>
                </select>
              </div>

              <div class="flex flex-col gap-1">
                <label class="text-xs font-semibold text-slate-700">Reference / Check #</label>
                <input
                  type="text"
                  pInputText
                  formControlName="referenceNumber"
                  placeholder="e.g. TXN-982103"
                  class="w-full"
                />
              </div>

              <div class="flex flex-col gap-1">
                <label class="text-xs font-semibold text-slate-700">Notes</label>
                <input
                  type="text"
                  pInputText
                  formControlName="notes"
                  placeholder="e.g. Paid at front desk"
                  class="w-full"
                />
              </div>

              <div class="flex justify-end gap-2 mt-4">
                <p-button label="Cancel" [text]="true" (onClick)="showPaymentDialog = false"></p-button>
                <p-button
                  type="submit"
                  label="Confirm Payment"
                  icon="pi pi-check"
                  severity="success"
                  [loading]="isSubmittingPayment()"
                  [disabled]="paymentForm.invalid || isSubmittingPayment()"
                ></p-button>
              </div>
            </form>
          </div>
        }
      </p-dialog>
    </div>
  `,
})
export class BillingList implements OnInit {
  private fb = inject(FormBuilder);
  private billingService = inject(BillingService);

  invoices = signal<Invoice[]>([]);
  isLoading = signal(true);

  searchQuery = signal<string>('');
  selectedStatus = signal<string>('');

  showPaymentDialog = false;
  selectedInvoice = signal<Invoice | null>(null);
  isSubmittingPayment = signal(false);
  paymentError = signal<string | null>(null);

  paymentForm = this.fb.group({
    amount: [0, [Validators.required, Validators.min(0.01)]],
    paymentMethod: [1, [Validators.required]],
    referenceNumber: [''],
    notes: [''],
  });

  filteredInvoices = computed(() => {
    let list = this.invoices();

    const query = this.searchQuery().toLowerCase().trim();
    if (query) {
      list = list.filter(
        (i) =>
          i.invoiceNumber.toLowerCase().includes(query) ||
          i.patientName.toLowerCase().includes(query)
      );
    }

    const status = this.selectedStatus();
    if (status) {
      list = list.filter((i) => i.status.toLowerCase() === status.toLowerCase());
    }

    return list;
  });

  totalCollected = computed(() =>
    this.invoices().reduce((acc, curr) => acc + curr.paidAmount, 0)
  );

  totalBalanceDue = computed(() =>
    this.invoices().reduce((acc, curr) => acc + curr.balanceDue, 0)
  );

  pendingCount = computed(() =>
    this.invoices().filter((i) => i.status === 'Pending' || i.status === 'PartiallyPaid').length
  );

  ngOnInit(): void {
    this.loadInvoices();
  }

  loadInvoices(): void {
    this.isLoading.set(true);
    this.billingService.getInvoices().subscribe({
      next: (data) => {
        this.invoices.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  openPaymentDialog(invoice: Invoice): void {
    this.selectedInvoice.set(invoice);
    this.paymentForm.patchValue({
      amount: invoice.balanceDue,
      paymentMethod: 1,
      referenceNumber: '',
      notes: '',
    });
    this.paymentError.set(null);
    this.showPaymentDialog = true;
  }

  onSavePayment(): void {
    if (this.paymentForm.invalid || !this.selectedInvoice()) return;

    this.isSubmittingPayment.set(true);
    this.paymentError.set(null);

    const val = this.paymentForm.value;
    const invId = this.selectedInvoice()!.id;

    this.billingService
      .recordPayment(invId, {
        amount: Number(val.amount),
        paymentMethod: Number(val.paymentMethod),
        referenceNumber: val.referenceNumber || '',
        notes: val.notes || '',
      })
      .subscribe({
        next: () => {
          this.isSubmittingPayment.set(false);
          this.showPaymentDialog = false;
          this.loadInvoices();
        },
        error: (err) => {
          this.isSubmittingPayment.set(false);
          this.paymentError.set(err.error?.detail || err.error?.message || 'Failed to record payment.');
        },
      });
  }

  getStatusSeverity(status: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (status.toLowerCase()) {
      case 'paid':
        return 'success';
      case 'partiallypaid':
        return 'info';
      case 'pending':
        return 'warn';
      case 'cancelled':
        return 'danger';
      default:
        return 'secondary';
    }
  }
}
