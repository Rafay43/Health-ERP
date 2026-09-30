using MediatR;
using HealthERP.Application.Laboratory.Models;

namespace HealthERP.Application.Laboratory.Commands.CreateLabOrder;

public record CreateLabOrderCommand(
    Guid PatientId,
    Guid? LabTestId,
    string OrderingDoctorName,
    string TestName,
    string TestCode,
    string Priority = "Routine"
) : IRequest<LabOrderDto>;
