using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Billing;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Appointments;

namespace HealthERP.Application.Billing.Commands.CreateInvoice;

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateInvoiceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(request.PatientId);
        var patientExists = await _context.Patients.AnyAsync(p => p.Id == patientId, cancellationToken);
        if (!patientExists)
        {
            throw new KeyNotFoundException($"Patient with ID '{request.PatientId}' was not found.");
        }

        AppointmentId? appointmentId = request.AppointmentId.HasValue
            ? new AppointmentId(request.AppointmentId.Value)
            : null;

        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var invoice = Invoice.Create(patientId, invoiceNumber, request.DueDateUtc, appointmentId);

        foreach (var item in request.LineItems)
        {
            invoice.AddLineItem(item.Description, item.Quantity, item.UnitPrice);
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return invoice.Id.Value;
    }
}
