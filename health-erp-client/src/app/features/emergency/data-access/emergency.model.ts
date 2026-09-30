export interface TriageRecord {
  id: string;
  patientId: string;
  patientName: string;
  patientMrn: string;
  chiefComplaint: string;
  bloodPressure: string;
  heartRateBpm: number;
  oxygenSaturationSpO2: number;
  bodyTemperatureCelsius: number;
  respiratoryRateBpm: number;
  severityName: string; // Level1_Resuscitation, Level2_Emergent, Level3_Urgent, Level4_LessUrgent, Level5_NonUrgent
  severityLevel: number; // 1-5
  statusName: string; // WaitingForTriage, Triaged, InBayCare, TransferredToInpatient, DischargedFromED
  statusValue: number;
  assignedBay?: string;
  triageNurseName: string;
  attendingPhysicianName?: string;
  arrivalDateUtc: string;
  dispositionDateUtc?: string;
  transferredBedId?: string;
}

export interface CreateTriageRecordRequest {
  patientId: string;
  chiefComplaint: string;
  bloodPressure: string;
  heartRateBpm: number;
  oxygenSaturationSpO2: number;
  bodyTemperatureCelsius: number;
  respiratoryRateBpm: number;
  severity: number; // 1-5
  triageNurseName: string;
  assignedBay?: string;
}

export interface AssignBayRequest {
  bayName: string;
  attendingPhysicianName: string;
}

export interface TransferInpatientRequest {
  bedId: string;
  admittingDoctorName: string;
  admissionNotes: string;
}
