export interface RegisterPatientRequest {
  medicalRecordNumber: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  gender: string;
  email: string;
  phoneNumber?: string;
}

export interface UpdatePatientRequest {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  gender: string;
  email: string;
  phoneNumber?: string;
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
  gender?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  isLinked?: boolean;
}

export interface NextMrnResponse {
  medicalRecordNumber: string;
}