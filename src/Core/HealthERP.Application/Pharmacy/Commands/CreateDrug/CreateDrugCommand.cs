using MediatR;
using HealthERP.Application.Pharmacy.Models;

namespace HealthERP.Application.Pharmacy.Commands.CreateDrug;

public record CreateDrugCommand(
    string Name,
    string GenericName,
    string Category,
    int StockQuantity,
    int ReorderLevel,
    DateTime ExpiryDateUtc,
    decimal UnitPrice
) : IRequest<DrugDto>;
