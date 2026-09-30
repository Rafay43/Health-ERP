export interface UserAccount {
  id: string;
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  isActive: boolean;
  createdAtUtc: string;
}

export interface AuditLogEntry {
  id: string;
  entityName: string;
  entityId: string;
  action: string;
  performedBy: string;
  timestampUtc: string;
  details: string;
}

export interface SystemHealth {
  status: string;
  databaseProvider: string;
  databaseCanConnect: boolean;
  totalUsers: number;
  activeRolesCount: number;
  serverTimeUtc: string;
}
