using MediatR;
using HealthERP.Application.Pharmacy.Models;

namespace HealthERP.Application.Pharmacy.Commands.DispensePrescription;

public record DispensePrescriptionCommand(
    Guid PrescriptionId,
    int QuantityToDeduct = 1
) : IRequest<PrescriptionDto>;
