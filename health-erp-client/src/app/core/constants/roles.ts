export const Roles = {
  Admin: 'Admin',
  Doctor: 'Doctor',
  Nurse: 'Nurse',
  Pharmacist: 'Pharmacist',
  LabTechnician: 'LabTechnician',
  Receptionist: 'Receptionist',
  BillingStaff: 'BillingStaff',
  Patient: 'Patient',
} as const;

export type UserRole = (typeof Roles)[keyof typeof Roles];
