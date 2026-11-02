using MediatR;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Pharmacy.Models;

namespace HealthERP.Application.Pharmacy.Commands.DispensePrescription;

public class DispensePrescriptionCommandHandler : IRequestHandler<DispensePrescriptionCommand, PrescriptionDto>
{
    private readonly IPharmacyDispensingService _dispensing;

    public DispensePrescriptionCommandHandler(IPharmacyDispensingService dispensing)
    {
        _dispensing = dispensing;
    }

    public async Task<PrescriptionDto> Handle(DispensePrescriptionCommand request, CancellationToken cancellationToken)
    {
        return await _dispensing.DispenseAsync(request.PrescriptionId, request.QuantityToDeduct, cancellationToken);
    }
}
