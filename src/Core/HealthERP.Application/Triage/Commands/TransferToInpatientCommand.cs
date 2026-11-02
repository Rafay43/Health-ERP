using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Triage;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Triage.Commands;

public record TransferToInpatientCommand(
    Guid TriageId,
    Guid BedId,
    string AdmittingDoctorName,
    string AdmissionNotes
) : IRequest<Guid>;

public class TransferToInpatientCommandHandler(IInpatientTransitionService transitions) : IRequestHandler<TransferToInpatientCommand, Guid>
{
    public Task<Guid> Handle(TransferToInpatientCommand request, CancellationToken cancellationToken) =>
        transitions.AdmitFromEmergencyAsync(request.TriageId, request.BedId, request.AdmittingDoctorName, request.AdmissionNotes, cancellationToken);
}
