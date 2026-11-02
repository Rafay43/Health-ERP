using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;

namespace HealthERP.Application.Patients.Queries.GetPatients;

public class GetPatientsQueryHandler : IRequestHandler<GetPatientsQuery, List<PatientDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPatientsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PatientDto>> Handle(GetPatientsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Patients
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PatientDto(
                p.Id.Value,
                p.MedicalRecordNumber,
                p.FirstName,
                p.LastName,
                p.DateOfBirth,
                p.Gender,
                p.Email,
                p.PhoneNumber,
                p.UserId != null))
            .ToListAsync(cancellationToken);
    }
}