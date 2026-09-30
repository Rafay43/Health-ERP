export interface InvoiceLineItem {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Payment {
  id: string;
  paymentDateUtc: string;
  amount: number;
  paymentMethod: string;
  referenceNumber?: string;
  notes?: string;
}

export interface Invoice {
  id: string;
  patientId: string;
  patientName: string;
  appointmentId?: string;
  invoiceNumber: string;
  issueDateUtc: string;
  dueDateUtc: string;
  status: 'Pending' | 'PartiallyPaid' | 'Paid' | 'Cancelled';
  totalAmount: number;
  paidAmount: number;
  balanceDue: number;
  lineItems: InvoiceLineItem[];
  payments: Payment[];
}

export interface RecordPaymentRequest {
  amount: number;
  paymentMethod: number; // 1: Cash, 2: CreditCard, 3: Insurance, 4: BankTransfer
  referenceNumber?: string;
  notes?: string;
}

export interface CreateInvoiceLineItemRequest {
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateInvoiceRequest {
  patientId: string;
  appointmentId?: string;
  dueDateUtc: string;
  lineItems: CreateInvoiceLineItemRequest[];
}
