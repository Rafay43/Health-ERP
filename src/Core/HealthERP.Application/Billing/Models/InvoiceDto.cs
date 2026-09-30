namespace HealthERP.Application.Billing.Models;

public record InvoiceDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid? AppointmentId,
    string InvoiceNumber,
    DateTime IssueDateUtc,
    DateTime DueDateUtc,
    string Status,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    List<InvoiceLineItemDto> LineItems,
    List<PaymentDto> Payments
);

public record InvoiceLineItemDto(
    Guid Id,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);

public record PaymentDto(
    Guid Id,
    DateTime PaymentDateUtc,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Notes
);
