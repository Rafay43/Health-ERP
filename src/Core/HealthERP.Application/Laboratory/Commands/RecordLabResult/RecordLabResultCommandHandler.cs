using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.Laboratory.Models;
using HealthERP.Domain.Laboratory;

namespace HealthERP.Application.Laboratory.Commands.RecordLabResult;

public class RecordLabResultCommandHandler : IRequestHandler<RecordLabResultCommand, LabOrderDto>
{
    private readonly IApplicationDbContext _context;

    public RecordLabResultCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LabOrderDto> Handle(RecordLabResultCommand request, CancellationToken cancellationToken)
    {
        var orderId = new LabOrderId(request.OrderId);
        var order = await _context.LabOrders
            .Include(o => o.Patient)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
            throw new KeyNotFoundException($"Lab order with ID '{request.OrderId}' was not found.");

        order.RecordResult(request.ResultValue, request.Remarks, request.PerformedBy, request.IsAbnormal);
        await _context.SaveChangesAsync(cancellationToken);

        return new LabOrderDto(
            order.Id.Value,
            order.PatientId.Value,
            order.Patient != null ? $"{order.Patient.FirstName} {order.Patient.LastName}" : "Unknown Patient",
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
