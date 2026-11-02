export interface Staff {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    phoneNumber: string;
    specialty: string;
    licenseNumber: string;
    department: string;
    isActive: boolean;
    staffKind?: string;
    linkedAccount?: StaffLinkedAccount | null;
}

export enum StaffKind { Unspecified = 0, Doctor = 1 }
export interface StaffLinkedAccount { userId: string; firstName: string; lastName: string; email: string | null; }
export interface StaffAccountCandidate extends StaffLinkedAccount { roles: string[]; isActive: boolean; isLinkedToStaff: boolean; }
export interface StaffAccountLookup { items: StaffAccountCandidate[]; page: number; pageSize: number; hasMore: boolean; }
export interface StaffAccountLink { staffId: string; staffKind: string; linkedAccount: StaffLinkedAccount | null; }

export interface CreateStaffRequest {
    firstName: string;
    lastName: string;
    email: string;
    phoneNumber: string;
    specialty: string;
    licenseNumber: string;
    department: string;
}
