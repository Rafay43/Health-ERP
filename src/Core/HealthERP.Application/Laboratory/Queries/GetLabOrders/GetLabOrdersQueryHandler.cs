using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Laboratory.Models;
using HealthERP.Domain.Patients;

namespace HealthERP.Application.Laboratory.Queries.GetLabOrders;

public class GetLabOrdersQueryHandler : IRequestHandler<GetLabOrdersQuery, List<LabOrderDto>>
{
    private readonly IApplicationDbContext _context;

    public GetLabOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LabOrderDto>> Handle(GetLabOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.LabOrders
            .Include(o => o.Patient)
            .Include(o => o.LabTest)
            .AsNoTracking();

        if (request.PatientId.HasValue)
        {
            var pId = new PatientId(request.PatientId.Value);
            query = query.Where(o => o.PatientId == pId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var statusStr = request.Status.Trim().ToLower();
            query = query.Where(o => o.Status.ToString().ToLower() == statusStr);
        }

        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            var prioStr = request.Priority.Trim().ToLower();
            query = query.Where(o => o.Priority.ToLower() == prioStr);
        }

        if (request.AbnormalOnly)
        {
            query = query.Where(o => o.IsAbnormal);
        }

        var orders = await query
            .OrderByDescending(o => o.OrderedDateUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return orders.Select(o => new LabOrderDto(
            o.Id.Value,
            o.PatientId.Value,
            o.Patient != null ? $"{o.Patient.FirstName} {o.Patient.LastName}" : "Unknown Patient",
            o.LabTestId?.Value,
            o.TestName,
            o.TestCode,
            o.OrderingDoctorName,
            o.Priority,
            o.Status.ToString(),
            o.OrderedDateUtc,
            o.ResultValue,
            o.ReferenceRange,
            o.Unit,
            o.Remarks,
            o.PerformedBy,
            o.CompletedDateUtc,
            o.IsAbnormal
        )).ToList();
    }
}
