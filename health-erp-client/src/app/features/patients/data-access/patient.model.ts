export interface RegisterPatientRequest {
  medicalRecordNumber: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
}

export interface RegisterPatientResponse {
  id: string;
}

export interface Patient {
  id: string;
  medicalRecordNumber: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
}

export interface NextMrnResponse {
  medicalRecordNumber: string;
}