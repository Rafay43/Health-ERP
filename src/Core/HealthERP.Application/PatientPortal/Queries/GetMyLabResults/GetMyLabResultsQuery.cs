using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.PatientPortal.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.PatientPortal.Queries.GetMyLabResults;

public record GetMyLabResultsQuery(
    int Page = 1,
    int PageSize = 25,
    bool CompletedOnly = true) : IRequest<List<PatientLabResultDto>>;

public class GetMyLabResultsQueryHandler : IRequestHandler<GetMyLabResultsQuery, List<PatientLabResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentPatientService _currentPatientService;

    public GetMyLabResultsQueryHandler(
        IApplicationDbContext context,
        ICurrentPatientService currentPatientService)
    {
        _context = context;
        _currentPatientService = currentPatientService;
    }

    public async Task<List<PatientLabResultDto>> Handle(GetMyLabResultsQuery request, CancellationToken cancellationToken)
    {
        var patient = await _currentPatientService.GetCurrentPatientAsync(cancellationToken);

        if (patient == null)
        {
            throw new UnauthorizedAccessException("Patient portal activation is required to access lab results.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.LabOrders
            .Where(l => l.PatientId == patient.Id);

        if (request.CompletedOnly)
        {
            query = query.Where(l => l.Status == HealthERP.Domain.Laboratory.LabOrderStatus.Completed);
        }

        return await query
            .OrderByDescending(l => l.OrderedDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new PatientLabResultDto(
                l.Id.Value,
                l.TestName,
                l.TestCode,
                l.Priority,
                l.OrderingDoctorName,
                l.OrderedDateUtc,
                l.Status.ToString(),
                l.ResultValue,
                l.ReferenceRange,
                l.Unit,
                l.PerformedBy,
                l.CompletedDateUtc,
                l.IsAbnormal))
            .ToListAsync(cancellationToken);
    }
}
