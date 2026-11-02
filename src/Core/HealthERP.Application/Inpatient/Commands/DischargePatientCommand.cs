using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Inpatient.Models;
using HealthERP.Domain.Inpatient;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Inpatient.Commands;

public record DischargePatientCommand(
    Guid AdmissionId,
    string DischargeSummary
) : IRequest<AdmissionDto>;

public class DischargePatientCommandHandler(IInpatientTransitionService transitions) : IRequestHandler<DischargePatientCommand, AdmissionDto>
{
    public Task<AdmissionDto> Handle(DischargePatientCommand request, CancellationToken cancellationToken) =>
        transitions.DischargeAsync(request.AdmissionId, request.DischargeSummary, cancellationToken);
}
