export interface PatientEhr {
  patientId: string;
  medicalRecordNumber: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  age: number;
  gender?: string;
  phoneNumber?: string;
  email?: string;
  latestVitals?: VitalsSummary;
  activeMedications: ActiveMedication[];
  pendingLabResults: PendingLabResult[];
  timeline: EhrTimelineEvent[];
}

export interface VitalsSummary {
  bloodPressure?: string;
  heartRateBpm?: number;
  temperatureCelsius?: number;
  weightKg?: number;
  recordedAtUtc: string;
}

export interface ActiveMedication {
  id: string;
  medicationName: string;
  dosage: string;
  frequency: string;
  durationDays: number;
  status: string;
  prescribedDateUtc: string;
}

export interface PendingLabResult {
  id: string;
  testName: string;
  testCode: string;
  orderingDoctorName: string;
  priority: string;
  status: string;
  orderedDateUtc: string;
}

export interface EhrTimelineEvent {
  id: string;
  eventType: 'Appointment' | 'Prescription' | 'LabOrder' | 'Invoice' | 'ClinicalNote';
  timestampUtc: string;
  title: string;
  description: string;
  status: string;
  badgeSeverity: 'success' | 'info' | 'warn' | 'danger' | 'secondary';
  icon: string;
  details?: any;
}

export interface AddClinicalNoteRequest {
  doctorName: string;
  chiefComplaint: string;
  diagnosis: string;
  notes: string;
  bloodPressure?: string;
  heartRateBpm?: number;
  temperatureCelsius?: number;
  weightKg?: number;
}
