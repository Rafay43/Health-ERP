using MediatR;
using HealthERP.Application.Pharmacy.Models;

namespace HealthERP.Application.Pharmacy.Queries.GetInventory;

public record GetInventoryQuery(
    string? SearchTerm = null,
    string? Category = null,
    bool LowStockOnly = false,
    bool ExpiringSoonOnly = false,
    int Page = 1,
    int PageSize = 50
) : IRequest<List<DrugDto>>;
