using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Laboratory.Models;

namespace HealthERP.Application.Laboratory.Queries.GetLabCatalog;

public class GetLabCatalogQueryHandler : IRequestHandler<GetLabCatalogQuery, List<LabTestDto>>
{
    private readonly IApplicationDbContext _context;

    public GetLabCatalogQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LabTestDto>> Handle(GetLabCatalogQuery request, CancellationToken cancellationToken)
    {
        var query = _context.LabTests.AsNoTracking();

        if (request.ActiveOnly)
        {
            query = query.Where(t => t.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(term) ||
                t.Code.ToLower().Contains(term) ||
                t.Category.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var cat = request.Category.Trim().ToLower();
            query = query.Where(t => t.Category.ToLower() == cat);
        }

        var tests = await query
            .OrderBy(t => t.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return tests.Select(t => new LabTestDto(
            t.Id.Value,
            t.Name,
            t.Code,
            t.Category,
            t.ReferenceRange,
            t.Unit,
            t.StandardPrice,
            t.SampleType,
            t.IsActive
        )).ToList();
    }
}
