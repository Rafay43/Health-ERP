using MediatR;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Laboratory.Models;
using HealthERP.Domain.Laboratory;

namespace HealthERP.Application.Laboratory.Commands.CreateLabTest;

public class CreateLabTestCommandHandler : IRequestHandler<CreateLabTestCommand, LabTestDto>
{
    private readonly IApplicationDbContext _context;

    public CreateLabTestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LabTestDto> Handle(CreateLabTestCommand request, CancellationToken cancellationToken)
    {
        var test = LabTest.Create(
            request.Name,
            request.Code,
            request.Category,
            request.ReferenceRange,
            request.Unit,
            request.StandardPrice,
            request.SampleType);

        _context.LabTests.Add(test);
        await _context.SaveChangesAsync(cancellationToken);

        return new LabTestDto(
            test.Id.Value,
            test.Name,
            test.Code,
            test.Category,
            test.ReferenceRange,
            test.Unit,
            test.StandardPrice,
            test.SampleType,
            test.IsActive);
    }
}
