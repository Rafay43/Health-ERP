using MediatR;
using HealthERP.Application.Laboratory.Models;

namespace HealthERP.Application.Laboratory.Commands.RecordLabResult;

public record RecordLabResultCommand(
    Guid OrderId,
    string ResultValue,
    string? Remarks,
    string PerformedBy,
    bool IsAbnormal = false
) : IRequest<LabOrderDto>;
