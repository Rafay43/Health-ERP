export interface LabTest {
  id: string;
  name: string;
  code: string;
  category: string;
  referenceRange: string;
  unit: string;
  standardPrice: number;
  sampleType: string;
  isActive: boolean;
}

export interface LabOrder {
  id: string;
  patientId: string;
  patientName: string;
  labTestId?: string;
  testName: string;
  testCode: string;
  orderingDoctorName: string;
  priority: string; // Routine, Urgent, STAT
  status: string; // Ordered, InProgress, Completed, Cancelled
  orderedDateUtc: string;
  resultValue?: string;
  referenceRange?: string;
  unit?: string;
  remarks?: string;
  performedBy?: string;
  completedDateUtc?: string;
  isAbnormal: boolean;
}

export interface CreateLabTestRequest {
  name: string;
  code: string;
  category: string;
  referenceRange: string;
  unit: string;
  standardPrice: number;
  sampleType: string;
}

export interface CreateLabOrderRequest {
  patientId: string;
  labTestId?: string;
  orderingDoctorName: string;
  testName: string;
  testCode: string;
  priority: string;
}

export interface RecordLabResultRequest {
  resultValue: string;
  remarks?: string;
  performedBy: string;
  isAbnormal: boolean;
}
