using MediatR;
using HealthERP.Application.Common.Interfaces;
using StaffEntity = HealthERP.Domain.StaffMembers.StaffMembers;

namespace HealthERP.Application.StaffMembers.Commands.CreateStaff;

public class CreateStaffCommandHandler : IRequestHandler<CreateStaffCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateStaffCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        var staff = StaffEntity.Create(
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.Specialty,
            request.LicenseNumber,
            request.Department);

        _context.Staff.Add(staff);

        var notification = HealthERP.Domain.Notifications.Notification.Create(
            "New Staff Member Registered",
            $"Staff member {request.FirstName} {request.LastName} ({request.Specialty}) was added to {request.Department} department.",
            HealthERP.Domain.Notifications.NotificationType.SystemInfo,
            HealthERP.Domain.Notifications.NotificationSeverity.Info,
            "/staff"
        );
        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync(cancellationToken);

        return staff.Id.Value;
    }
}
