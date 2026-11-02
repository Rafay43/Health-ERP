using HealthERP.Domain.StaffMembers;

namespace HealthERP.Application.StaffMembers.Models;

public sealed record CurrentStaffContext(StaffId StaffId, Guid UserId, StaffKind StaffKind);
