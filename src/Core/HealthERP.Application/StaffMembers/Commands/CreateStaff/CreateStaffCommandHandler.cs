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
        await _context.SaveChangesAsync(cancellationToken);

        return staff.Id.Value;
    }
}
