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

        var beds = await query
            .OrderBy(b => b.WardName)
            .ThenBy(b => b.RoomNumber)
            .ThenBy(b => b.BedNumber)
            .ToListAsync(cancellationToken);

        var activeAdmissions = await _context.Admissions
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => !a.IsDischarged)
            .ToListAsync(cancellationToken);

        var admissionMap = activeAdmissions.ToDictionary(a => a.BedId);

        var result = beds.Select(b =>
        {
            admissionMap.TryGetValue(b.Id, out var admission);
            var patientName = admission != null && admission.Patient != null
                ? $"{admission.Patient.FirstName} {admission.Patient.LastName}"
                : null;
            var patientMrn = admission?.Patient?.MedicalRecordNumber;

            return new BedDto(
                b.Id.Value,
                b.WardName,
                b.RoomNumber,
                b.BedNumber,
                b.BedType,
                b.DailyRate,
                b.Status.ToString(),
                (int)b.Status,
                admission?.Id.Value,
                patientName,
                patientMrn
            );
        }).ToList();

        return result;
    }
}
