using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;

namespace HealthERP.Application.StaffMembers.Queries.GetDoctorsLookup;

public record DoctorLookupDto(
    Guid Id,
    string FullName,
    string Specialty,
    string Department
);

public record GetDoctorsLookupQuery : IRequest<List<DoctorLookupDto>>;

public class GetDoctorsLookupQueryHandler : IRequestHandler<GetDoctorsLookupQuery, List<DoctorLookupDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDoctorsLookupQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DoctorLookupDto>> Handle(GetDoctorsLookupQuery request, CancellationToken cancellationToken)
    {
        return await _context.Staff
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsActive)
            .Select(s => new DoctorLookupDto(
                s.Id.Value,
                $"Dr. {s.FirstName} {s.LastName}",
                s.Specialty ?? "General Practice",
                s.Department ?? "Outpatient"))
            .ToListAsync(cancellationToken);
    }
}
