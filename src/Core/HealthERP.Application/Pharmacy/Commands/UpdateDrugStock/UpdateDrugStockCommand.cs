using MediatR;
using HealthERP.Application.Pharmacy.Models;

namespace HealthERP.Application.Pharmacy.Commands.UpdateDrugStock;

public record UpdateDrugStockCommand(
    Guid DrugId,
    int NewQuantity
) : IRequest<DrugDto>;
