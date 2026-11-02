using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.StaffMembers.Models;

namespace HealthERP.Application.StaffMembers.Queries.GetStaff;

public record GetStaffQuery : IRequest<List<StaffDto>>;

public class GetStaffQueryHandler : IRequestHandler<GetStaffQuery, List<StaffDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IStaffAccountService _accounts;

    public GetStaffQueryHandler(IApplicationDbContext context, IStaffAccountService accounts)
    {
        _context = context;
        _accounts = accounts;
    }

    public async Task<List<StaffDto>> Handle(GetStaffQuery request, CancellationToken cancellationToken)
    {
        var staff = await _context.Staff
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
                s.IsActive,
                s.StaffKind.ToString(),
                null))
            .ToListAsync(cancellationToken);
        var accounts = await _accounts.GetLinkedAccountsAsync(staff.Select(s => s.Id), cancellationToken);
        return staff.Select(s => s with { LinkedAccount = accounts.GetValueOrDefault(s.Id) }).ToList();
    }
}
