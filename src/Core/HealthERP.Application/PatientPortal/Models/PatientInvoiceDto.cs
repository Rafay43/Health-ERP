namespace HealthERP.Application.PatientPortal.Models;

public record PatientInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    DateTime IssueDateUtc,
    DateTime DueDateUtc,
    string Status,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    List<PatientInvoiceLineItemDto> LineItems,
    List<PatientPaymentDto> Payments);

public record PatientInvoiceLineItemDto(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

public record PatientPaymentDto(
    decimal Amount,
    string PaymentMethod,
    DateTime PaymentDateUtc);
