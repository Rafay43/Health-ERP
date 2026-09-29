using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.StaffMembers.Models;

namespace HealthERP.Application.StaffMembers.Queries.GetStaff;

public record GetStaffQuery : IRequest<List<StaffDto>>;

public class GetStaffQueryHandler : IRequestHandler<GetStaffQuery, List<StaffDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStaffQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StaffDto>> Handle(GetStaffQuery request, CancellationToken cancellationToken)
    {
        return await _context.Staff
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Select(s => new StaffDto(
                s.Id.Value,
                s.FirstName,
                s.LastName,
                s.Email,
                s.PhoneNumber,
                s.Specialty,
                s.LicenseNumber,
                s.Department,
                s.IsActive))
            .ToListAsync(cancellationToken);
    }
}
