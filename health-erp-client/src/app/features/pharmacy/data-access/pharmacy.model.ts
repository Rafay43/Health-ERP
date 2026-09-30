export interface Drug {
  id: string;
  name: string;
  genericName: string;
  category: string;
  stockQuantity: number;
  reorderLevel: number;
  expiryDateUtc: string;
  unitPrice: number;
  isLowStock: boolean;
  isExpiringSoon: boolean;
}

export interface Prescription {
  id: string;
  patientId: string;
  patientName: string;
  drugId?: string;
  drugName?: string;
  doctorName: string;
  medicationName: string;
  dosage: string;
  frequency: string;
  durationDays: number;
  refills: number;
  instructions?: string;
  status: string;
  prescribedDateUtc: string;
  dispensedDateUtc?: string;
}

export interface CreateDrugRequest {
  name: string;
  genericName: string;
  category: string;
  stockQuantity: number;
  reorderLevel: number;
  expiryDateUtc: string;
  unitPrice: number;
}

export interface CreatePrescriptionRequest {
  patientId: string;
  drugId?: string;
  doctorName: string;
  medicationName: string;
  dosage: string;
  frequency: string;
  durationDays: number;
  refills: number;
  instructions?: string;
}
