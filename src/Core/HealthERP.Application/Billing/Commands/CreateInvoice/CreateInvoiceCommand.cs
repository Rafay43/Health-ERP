using MediatR;

namespace HealthERP.Application.Billing.Commands.CreateInvoice;

public record CreateInvoiceLineItemRequest(
    string Description,
    int Quantity,
    decimal UnitPrice
);

public record CreateInvoiceCommand(
    Guid PatientId,
    Guid? AppointmentId,
    DateTime DueDateUtc,
    List<CreateInvoiceLineItemRequest> LineItems
) : IRequest<Guid>;
