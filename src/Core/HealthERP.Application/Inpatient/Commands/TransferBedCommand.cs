using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Inpatient.Models;
using HealthERP.Domain.Inpatient;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Inpatient.Commands;

public record TransferBedCommand(
    Guid AdmissionId,
    Guid NewBedId,
    string? TransferReason
) : IRequest<AdmissionDto>;

public class TransferBedCommandHandler(IInpatientTransitionService transitions) : IRequestHandler<TransferBedCommand, AdmissionDto>
{
    public Task<AdmissionDto> Handle(TransferBedCommand request, CancellationToken cancellationToken) =>
        transitions.TransferAsync(request.AdmissionId, request.NewBedId, request.TransferReason, cancellationToken);
}
