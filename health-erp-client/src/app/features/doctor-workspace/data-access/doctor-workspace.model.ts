export type ScheduleStatus = 'Scheduled' | 'Confirmed' | 'Completed' | 'Cancelled' | 'NoShow';

export interface DoctorScheduleAppointment {
  appointmentId: string;
  patientId: string;
  patientMedicalRecordNumber: string | null;
  patientDisplayName: string | null;
  startDateTimeUtc: string;
  endDateTimeUtc: string;
  status: ScheduleStatus;
  reason: string;
}

export interface DoctorSchedule {
  date: string;
  timeZoneId: string;
  doctor: { displayName: string; specialty: string; department: string };
  summary: { total: number; scheduled: number; confirmed: number; completed: number; cancelled: number; noShow: number };
  nextScheduledConsultation: DoctorScheduleAppointment | null;
  appointments: DoctorScheduleAppointment[];
}

export interface DoctorPatientSnapshot {
  patient: { patientId: string; medicalRecordNumber: string; displayName: string; dateOfBirth: string; gender: string | null };
  latestVitals: {
    bloodPressure: string | null; heartRateBpm: number | null; temperatureCelsius: number | null;
    weightKg: number | null; recordedAtUtc: string;
  } | null;
  recentNotes: { noteId: string; recordedAtUtc: string; chiefComplaint: string; diagnosis: string; notes: string; recordedByDisplayName: string | null }[];
  recentPrescriptions: {
    prescriptionId: string; prescribedDateUtc: string; medicationName: string; dosage: string; frequency: string;
    durationDays: number; refills: number; instructions: string | null;
    status: 'Pending' | 'Dispensed' | 'Cancelled'; dispensedDateUtc: string | null;
  }[];
  recentLabOrders: {
    labOrderId: string; orderedDateUtc: string; testName: string; testCode: string; priority: string;
    status: 'Ordered' | 'InProgress' | 'Completed' | 'Cancelled';
    result: {
      completedDateUtc: string | null; value: string | null; referenceRange: string | null;
      unit: string | null; remarks: string | null; isAbnormal: boolean;
    } | null;
  }[];
  currentAdmission: { admissionId: string; admissionDateUtc: string; chiefComplaint: string } | null;
}

export interface DoctorClinicalNoteInput {
  chiefComplaint: string | null; diagnosis: string | null; notes: string | null; bloodPressure: string | null;
  heartRateBpm: number | null; temperatureCelsius: number | null; weightKg: number | null;
}
