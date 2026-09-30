using MediatR;
using HealthERP.Application.Billing.Queries.GetInvoices;
using HealthERP.Application.Billing.Commands.CreateInvoice;
using HealthERP.Application.Billing.Commands.RecordPayment;
using HealthERP.Application.Billing.Models;
using HealthERP.Domain.Billing;

namespace HealthERP.Api.Endpoints;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/invoices")
            .WithTags("Billing")
            .RequireAuthorization();

        group.MapGet("/", async (IMediator mediator, int page = 1, int pageSize = 25, string? status = null, Guid? patientId = null) =>
        {
            var invoices = await mediator.Send(new GetInvoicesQuery(page, pageSize, status, patientId));
            return Results.Ok(invoices);
        })
        .WithName("GetInvoices")
        .Produces<List<InvoiceDto>>(200);

        group.MapPost("/", async (CreateInvoiceCommand command, IMediator mediator) =>
        {
            var invoiceId = await mediator.Send(command);
            return Results.Created($"/api/v1/invoices/{invoiceId}", new { id = invoiceId });
        })
        .WithName("CreateInvoice")
        .Produces(201)
        .Produces(400);

        group.MapPost("/{id:guid}/payments", async (Guid id, RecordPaymentRequest request, IMediator mediator) =>
        {
            await mediator.Send(new RecordPaymentCommand(id, request.Amount, request.PaymentMethod, request.ReferenceNumber, request.Notes));
            return Results.NoContent();
        })
        .WithName("RecordPayment")
        .Produces(204)
        .Produces(400)
        .Produces(404);
    }
}

public record RecordPaymentRequest(
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber = null,
    string? Notes = null
);
