using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Pharmacy.Models;
using HealthERP.Domain.Pharmacy;

namespace HealthERP.Application.Pharmacy.Commands.UpdateDrugStock;

public class UpdateDrugStockCommandHandler : IRequestHandler<UpdateDrugStockCommand, DrugDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateDrugStockCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DrugDto> Handle(UpdateDrugStockCommand request, CancellationToken cancellationToken)
    {
        var drugId = new DrugId(request.DrugId);
        var drug = await _context.Drugs.FirstOrDefaultAsync(d => d.Id == drugId, cancellationToken);

        if (drug == null)
            throw new KeyNotFoundException($"Drug with ID '{request.DrugId}' was not found.");

        drug.UpdateStock(request.NewQuantity);
        await _context.SaveChangesAsync(cancellationToken);

        return new DrugDto(
            drug.Id.Value,
            drug.Name,
            drug.GenericName,
            drug.Category,
            drug.StockQuantity,
            drug.ReorderLevel,
            drug.ExpiryDateUtc,
            drug.UnitPrice,
            drug.IsLowStock,
            drug.IsExpiringSoon);
    }
}
