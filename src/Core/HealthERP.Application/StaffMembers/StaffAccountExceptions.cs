namespace HealthERP.Application.StaffMembers;

public sealed class StaffAccountConflictException(string message) : Exception(message);
public sealed class StaffAccountStorageException(Exception inner) : Exception("Unable to confirm staff account change.", inner);
public sealed class StaffAccountForbiddenException : Exception;
