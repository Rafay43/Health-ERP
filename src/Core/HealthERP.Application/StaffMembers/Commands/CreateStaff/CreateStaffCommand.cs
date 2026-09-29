using MediatR;

namespace HealthERP.Application.StaffMembers.Commands.CreateStaff;

public record CreateStaffCommand(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Specialty,
    string LicenseNumber,
    string Department
) : IRequest<Guid>;
