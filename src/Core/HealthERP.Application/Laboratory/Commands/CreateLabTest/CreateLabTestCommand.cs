using MediatR;
using HealthERP.Application.Laboratory.Models;

namespace HealthERP.Application.Laboratory.Commands.CreateLabTest;

public record CreateLabTestCommand(
    string Name,
    string Code,
    string Category,
    string ReferenceRange,
    string Unit,
    decimal StandardPrice,
    string SampleType
) : IRequest<LabTestDto>;
