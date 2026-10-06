export interface Bed {
  id: string;
  wardName: string;
  roomNumber: string;
  bedNumber: string;
  bedType: string;
  dailyRate: number;
  status: string; // Available, Occupied, Maintenance, Cleaning
  statusValue: number;
  currentAdmissionId?: string;
  occupiedByPatientName?: string;
  occupiedByPatientMrn?: string;
}

export interface Admission {
  id: string;
  patientId: string;
  patientName: string;
  patientMrn: string;
  bedId: string;
  wardName: string;
  roomNumber: string;
  bedNumber: string;
  admittingDoctorName: string;
  chiefComplaint: string;
  admissionDateUtc: string;
  dischargeDateUtc?: string;
  dischargeSummary?: string;
  dailyRateSnapshot: number;
  totalRoomCharge: number;
  isDischarged: boolean;
}

export interface CreateBedRequest {
  wardName: string;
  roomNumber: string;
  bedNumber: string;
  bedType: string;
  dailyRate: number;
}

export interface AdmitPatientRequest {
  patientId: string;
  bedId: string;
  admittingDoctorName: string;
  chiefComplaint: string;
}

export interface DischargeRequest {
  dischargeSummary: string;
}

export interface TransferBedRequest {
  newBedId: string;
  transferReason?: string;
}

