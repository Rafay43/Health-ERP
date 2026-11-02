import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PatientPortalService, PatientInvoiceDto } from '../../../core/services/patient-portal.service';

@Component({
  selector: 'app-portal-billing',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="space-y-6">

      <!-- PAGE HEADER -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 class="text-2xl font-bold text-[#0F172A] tracking-tight">My Billing</h2>
          <p class="text-sm text-[#64748B] mt-0.5">Read-only account statement, invoice details, and payment history.</p>
        </div>
      </div>

      <!-- LOADING SKELETON -->
      <div *ngIf="isLoading()" class="space-y-4">
        <div *ngFor="let i of [1,2]" class="bg-white p-6 rounded-2xl border border-slate-200 animate-pulse space-y-3">
          <div class="h-6 bg-slate-200 rounded w-1/3"></div>
          <div class="h-4 bg-slate-200 rounded w-1/2"></div>
        </div>
      </div>

      <div *ngIf="!isLoading()" class="space-y-6">

        <!-- BILLING SUMMARY METRICS CARD -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-5">
          <div class="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs flex items-center justify-between">
            <div>
              <span class="text-xs font-bold uppercase tracking-wider text-slate-400">Total Outstanding Balance</span>
              <div class="text-3xl font-extrabold text-[#0F172A] mt-1">
                {{ totalOutstanding() | currency:'USD':'symbol':'1.2-2' }}
              </div>
            </div>
            <div class="w-12 h-12 rounded-xl bg-amber-50 text-amber-600 flex items-center justify-center text-xl">
              <i class="pi pi-wallet"></i>
            </div>
          </div>

          <div class="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs flex items-center justify-between">
            <div>
              <span class="text-xs font-bold uppercase tracking-wider text-slate-400">Total Payments Made</span>
              <div class="text-3xl font-extrabold text-emerald-700 mt-1">
                {{ totalPaid() | currency:'USD':'symbol':'1.2-2' }}
              </div>
            </div>
            <div class="w-12 h-12 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center text-xl">
              <i class="pi pi-check-circle"></i>
            </div>
          </div>
        </div>

        <!-- INVOICE CARDS LIST -->
        <div *ngIf="invoices().length > 0" class="space-y-4">
          <h3 class="text-base font-bold text-[#0F172A]">Billing Statements</h3>

          <div
            *ngFor="let inv of invoices()"
            class="bg-white rounded-2xl border border-slate-200/80 shadow-xs hover:border-teal-300 transition-all overflow-hidden"
          >
            <!-- INVOICE HEADER ROW -->
            <div class="p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-100 bg-slate-50/40">
              <div>
                <div class="flex items-center gap-3">
                  <span class="text-base font-bold text-[#0F172A]">{{ inv.invoiceNumber }}</span>
                  <span
                    class="px-2.5 py-0.5 rounded-full text-xs font-semibold"
                    [ngClass]="getStatusBadgeClass(inv.status)"
                  >
                    {{ inv.status }}
                  </span>
                </div>
                <span class="text-xs text-slate-500 mt-0.5 block font-medium">
                  Issued: {{ inv.issueDateUtc | date:'mediumDate' }} &bull; Due: {{ inv.dueDateUtc | date:'mediumDate' }}
                </span>
              </div>

              <div class="flex items-center gap-6">
                <div class="text-right">
                  <span class="text-xs text-slate-400 block font-medium">Balance Due</span>
                  <span class="text-lg font-bold text-[#0F172A]">{{ inv.balanceDue | currency:'USD':'symbol':'1.2-2' }}</span>
                </div>
                <button
                  (click)="toggleExpand(inv.id)"
                  class="p-2 rounded-lg text-slate-500 hover:bg-slate-200/60 transition-colors cursor-pointer"
                  title="View Line Items"
                >
                  <i class="pi" [ngClass]="expandedInvoiceId() === inv.id ? 'pi-chevron-up' : 'pi-chevron-down'"></i>
                </button>
              </div>
            </div>

            <!-- EXPANDABLE LINE ITEMS & PAYMENTS -->
            <div *ngIf="expandedInvoiceId() === inv.id" class="p-5 space-y-4 bg-white border-t border-slate-100">
              <div>
                <h4 class="text-xs font-bold uppercase tracking-wider text-slate-400 mb-2">Itemized Services</h4>
                <div class="space-y-2">
                  <div
                    *ngFor="let item of inv.lineItems"
                    class="flex items-center justify-between text-xs py-2 px-3 bg-slate-50 rounded-lg border border-slate-200/50"
                  >
                    <div>
                      <span class="font-bold text-[#0F172A] block">{{ item.description }}</span>
                      <span class="text-slate-400">Qty: {{ item.quantity }} &times; {{ item.unitPrice | currency }}</span>
                    </div>
                    <span class="font-bold text-[#0F172A]">{{ item.totalPrice | currency }}</span>
                  </div>
                </div>
              </div>

              <div *ngIf="inv.payments && inv.payments.length > 0">
                <h4 class="text-xs font-bold uppercase tracking-wider text-slate-400 mb-2">Payment Receipts</h4>
                <div class="space-y-2">
                  <div
                    *ngFor="let p of inv.payments"
                    class="flex items-center justify-between text-xs py-2 px-3 bg-emerald-50/60 rounded-lg border border-emerald-200/50"
                  >
                    <div class="flex items-center gap-2">
                      <i class="pi pi-check-circle text-emerald-600"></i>
                      <span class="font-medium text-emerald-900">Paid via {{ p.paymentMethod }} on {{ p.paymentDateUtc | date:'mediumDate' }}</span>
                    </div>
                    <span class="font-bold text-emerald-800">{{ p.amount | currency }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- EMPTY STATE -->
        <div *ngIf="invoices().length === 0" class="bg-white p-12 rounded-2xl border border-slate-200 text-center max-w-md mx-auto my-8">
          <div class="w-14 h-14 rounded-full bg-slate-100 text-slate-400 flex items-center justify-center mx-auto mb-4 text-2xl">
            <i class="pi pi-wallet"></i>
          </div>
          <h3 class="text-base font-bold text-[#0F172A] mb-1">No billing records</h3>
          <p class="text-xs text-slate-500">
            You currently have no invoices or billing statements on file.
          </p>
        </div>

      </div>

    </div>
  `,
})
export class PortalBillingComponent implements OnInit {
  private portalService = inject(PatientPortalService);

  isLoading = signal(true);
  invoices = signal<PatientInvoiceDto[]>([]);
  expandedInvoiceId = signal<string | null>(null);

  totalOutstanding = computed(() => {
    return this.invoices().reduce((acc, inv) => acc + (inv.balanceDue || 0), 0);
  });

  totalPaid = computed(() => {
    return this.invoices().reduce((acc, inv) => acc + (inv.paidAmount || 0), 0);
  });

  ngOnInit(): void {
    this.loadBilling();
  }

  loadBilling(): void {
    this.isLoading.set(true);
    this.portalService.getBilling().subscribe({
      next: (data) => {
        this.invoices.set(data || []);
        if (data && data.length > 0) {
          this.expandedInvoiceId.set(data[0].id);
        }
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }

  toggleExpand(id: string): void {
    if (this.expandedInvoiceId() === id) {
      this.expandedInvoiceId.set(null);
    } else {
      this.expandedInvoiceId.set(id);
    }
  }

  getStatusBadgeClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'paid':
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      case 'unpaid':
      case 'overdue':
        return 'bg-rose-50 text-rose-700 border border-rose-200';
      case 'partiallypaid':
        return 'bg-amber-50 text-amber-700 border border-amber-200';
      default:
        return 'bg-slate-100 text-slate-700 border border-slate-200';
    }
  }
}
