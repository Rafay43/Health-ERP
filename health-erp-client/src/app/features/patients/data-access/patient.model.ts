export interface RegisterPatientRequest {
  medicalRecordNumber: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string; // ISO format: YYYY-MM-DD
}

export interface RegisterPatientResponse {
  id: string;
}