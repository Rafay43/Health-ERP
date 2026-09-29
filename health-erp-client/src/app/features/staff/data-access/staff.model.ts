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
}

export interface CreateStaffRequest {
    firstName: string;
    lastName: string;
    email: string;
    phoneNumber: string;
    specialty: string;
    licenseNumber: string;
    department: string;
}
