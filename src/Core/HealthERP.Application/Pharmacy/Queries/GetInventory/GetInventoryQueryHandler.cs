using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Pharmacy.Models;

namespace HealthERP.Application.Pharmacy.Queries.GetInventory;

public class GetInventoryQueryHandler : IRequestHandler<GetInventoryQuery, List<DrugDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DrugDto>> Handle(GetInventoryQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Drugs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(d =>
                d.Name.ToLower().Contains(term) ||
                d.GenericName.ToLower().Contains(term) ||
                d.Category.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var cat = request.Category.Trim().ToLower();
            query = query.Where(d => d.Category.ToLower() == cat);
        }

        if (request.LowStockOnly)
        {
            query = query.Where(d => d.StockQuantity <= d.ReorderLevel);
        }

        if (request.ExpiringSoonOnly)
        {
            var warningThreshold = DateTime.UtcNow.AddDays(30);
            query = query.Where(d => d.ExpiryDateUtc <= warningThreshold);
        }

        var drugs = await query
            .OrderBy(d => d.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return drugs.Select(d => new DrugDto(
            d.Id.Value,
            d.Name,
            d.GenericName,
            d.Category,
            d.StockQuantity,
            d.ReorderLevel,
            d.ExpiryDateUtc,
            d.UnitPrice,
            d.IsLowStock,
            d.IsExpiringSoon
        )).ToList();
    }
}
