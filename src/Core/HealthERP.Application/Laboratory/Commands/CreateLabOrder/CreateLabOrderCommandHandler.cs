using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Laboratory.Models;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Laboratory;

namespace HealthERP.Application.Laboratory.Commands.CreateLabOrder;

public class CreateLabOrderCommandHandler : IRequestHandler<CreateLabOrderCommand, LabOrderDto>
{
    private readonly IApplicationDbContext _context;

    public CreateLabOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LabOrderDto> Handle(CreateLabOrderCommand request, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(request.PatientId);
        var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient == null)
            throw new KeyNotFoundException($"Patient with ID '{request.PatientId}' was not found.");

        LabTestId? testId = request.LabTestId.HasValue ? new LabTestId(request.LabTestId.Value) : null;
        string testName = request.TestName;
        string testCode = request.TestCode;
        string? refRange = null;
        string? unit = null;

        if (testId.HasValue)
        {
            var test = await _context.LabTests.FirstOrDefaultAsync(t => t.Id == testId.Value, cancellationToken);
            if (test != null)
            {
                testName = test.Name;
                testCode = test.Code;
                refRange = test.ReferenceRange;
                unit = test.Unit;
            }
        }

        var order = LabOrder.Create(
            patientId,
            testId,
            request.OrderingDoctorName,
            testName,
            testCode,
            request.Priority,
            refRange,
            unit);

        _context.LabOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        return new LabOrderDto(
            order.Id.Value,
            order.PatientId.Value,
            $"{patient.FirstName} {patient.LastName}",
            order.LabTestId?.Value,
            order.TestName,
            order.TestCode,
            order.OrderingDoctorName,
            order.Priority,
            order.Status.ToString(),
            order.OrderedDateUtc,
            order.ResultValue,
            order.ReferenceRange,
            order.Unit,
            order.Remarks,
            order.PerformedBy,
            order.CompletedDateUtc,
            order.IsAbnormal);
    }
}
