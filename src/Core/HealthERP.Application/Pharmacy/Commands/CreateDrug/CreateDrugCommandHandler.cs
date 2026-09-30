using MediatR;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Pharmacy.Models;
using HealthERP.Domain.Pharmacy;

namespace HealthERP.Application.Pharmacy.Commands.CreateDrug;

public class CreateDrugCommandHandler : IRequestHandler<CreateDrugCommand, DrugDto>
{
    private readonly IApplicationDbContext _context;

    public CreateDrugCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DrugDto> Handle(CreateDrugCommand request, CancellationToken cancellationToken)
    {
        var drug = Drug.Create(
            request.Name,
            request.GenericName,
            request.Category,
            request.StockQuantity,
            request.ReorderLevel,
            request.ExpiryDateUtc.ToUniversalTime(),
            request.UnitPrice);

        _context.Drugs.Add(drug);
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
