namespace HealthERP.Application.StaffMembers;

public sealed class StaffAccessDeniedException : Exception
{
    public StaffAccessDeniedException() : base("An eligible staff association is required for this operation.") { }
}
