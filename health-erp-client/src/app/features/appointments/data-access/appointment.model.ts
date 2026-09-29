export interface Appointment {
    id: string;
    patientId: string;
    patientName: string;
    staffId: string;
    doctorName: string;
    startDateTimeUtc: string;
    endDateTimeUtc: string;
    reason: string;
    notes?: string;
    status: string;
}

export interface BookAppointmentRequest {
    patientId: string;
    staffId: string;
    startDateTimeUtc: string;
    endDateTimeUtc: string;
    reason: string;
    notes?: string;
}
