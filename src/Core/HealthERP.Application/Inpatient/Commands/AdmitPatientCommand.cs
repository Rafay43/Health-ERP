using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Patients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Inpatient.Commands;

public record AdmitPatientCommand(
    Guid PatientId,
    Guid BedId,
    string AdmittingDoctorName,
    string ChiefComplaint
) : IRequest<Guid>;

public class AdmitPatientCommandHandler(IInpatientTransitionService transitions) : IRequestHandler<AdmitPatientCommand, Guid>
{
    public Task<Guid> Handle(AdmitPatientCommand request, CancellationToken cancellationToken) =>
        transitions.AdmitAsync(request.PatientId, request.BedId, request.AdmittingDoctorName, request.ChiefComplaint, cancellationToken);
}
