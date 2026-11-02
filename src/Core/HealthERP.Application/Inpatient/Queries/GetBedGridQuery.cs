using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Inpatient.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Inpatient.Queries;

public record GetBedGridQuery(string? WardName = null) : IRequest<List<BedDto>>;

public class GetBedGridQueryHandler : IRequestHandler<GetBedGridQuery, List<BedDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBedGridQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BedDto>> Handle(GetBedGridQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Beds.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.WardName))
        {
            query = query.Where(b => b.WardName.ToLower().Contains(request.WardName.ToLower()));
        }

        // Beds, active owners and patient details must come from one SQL statement/snapshot.
        var rows = await (
            from bed in query
            join admission in _context.Admissions.AsNoTracking().Where(a => !a.IsDischarged)
                on bed.Id equals admission.BedId into occupants
            from admission in occupants.DefaultIfEmpty()
            orderby bed.WardName, bed.RoomNumber, bed.BedNumber
            select new { Bed = bed, Admission = admission, Patient = admission == null ? null : admission.Patient }
        ).ToListAsync(cancellationToken);

        if (rows.GroupBy(r => r.Bed.Id).Any(g => g.Count() > 1))
            throw new HealthERP.Application.Inpatient.InpatientConflictException("Bed occupancy requires reconciliation. Refresh and contact staff support.");

        return rows.Select(r => new BedDto(
            r.Bed.Id.Value, r.Bed.WardName, r.Bed.RoomNumber, r.Bed.BedNumber, r.Bed.BedType,
            r.Bed.DailyRate, r.Bed.Status.ToString(), (int)r.Bed.Status, r.Admission?.Id.Value,
            r.Patient is null ? null : $"{r.Patient.FirstName} {r.Patient.LastName}",
            r.Patient?.MedicalRecordNumber
        )).ToList();
    }
}
