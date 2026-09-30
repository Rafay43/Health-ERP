using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Appointments;

namespace HealthERP.Application.Appointments.Commands.UpdateAppointmentStatus;

public class UpdateAppointmentStatusCommandHandler : IRequestHandler<UpdateAppointmentStatusCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IAppointmentNotificationService _notificationService;

    public UpdateAppointmentStatusCommandHandler(
        IApplicationDbContext context,
        IAppointmentNotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task Handle(UpdateAppointmentStatusCommand request, CancellationToken cancellationToken)
    {
        var appointmentId = new AppointmentId(request.Id);

        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(a => a.Id == appointmentId && !a.IsDeleted, cancellationToken);

        if (appointment is null)
        {
            throw new KeyNotFoundException($"Appointment with ID '{request.Id}' was not found.");
        }

        switch (request.Status)
        {
            case AppointmentStatus.Confirmed:
                appointment.Confirm();
                break;
            case AppointmentStatus.Completed:
                appointment.Complete();
                await AutoGenerateConsultationInvoiceAsync(appointment, cancellationToken);
                break;
            case AppointmentStatus.Cancelled:
                appointment.Cancel();
                break;
            case AppointmentStatus.NoShow:
                appointment.MarkNoShow();
                break;
            default:
                throw new ArgumentException($"Invalid appointment status '{request.Status}'.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyAppointmentChangedAsync(cancellationToken);
    }

    private async Task AutoGenerateConsultationInvoiceAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        var hasInvoice = await _context.Invoices
            .AnyAsync(i => i.AppointmentId == appointment.Id, cancellationToken);

        if (hasInvoice) return;

        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var invoice = HealthERP.Domain.Billing.Invoice.Create(
            appointment.PatientId,
            invoiceNumber,
            DateTime.UtcNow.AddDays(30),
            appointment.Id);

        invoice.AddLineItem($"Medical Consultation Fee ({appointment.Reason})", 1, 150.00m);

        _context.Invoices.Add(invoice);
    }
}
