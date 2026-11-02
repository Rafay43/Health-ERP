using MediatR;
using HealthERP.Application.Pharmacy.Queries.GetInventory;
using HealthERP.Application.Pharmacy.Queries.GetPrescriptions;
using HealthERP.Application.Pharmacy.Commands.CreateDrug;
using HealthERP.Application.Pharmacy.Commands.UpdateDrugStock;
using HealthERP.Application.Pharmacy.Commands.CreatePrescription;
using HealthERP.Application.Pharmacy.Commands.DispensePrescription;
using HealthERP.Application.Pharmacy.Models;

namespace HealthERP.Api.Endpoints;

public static class PharmacyEndpoints
{
    public static void MapPharmacyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pharmacy")
            .WithTags("Pharmacy");

        group.MapGet("/inventory", async (
            IMediator mediator,
            string? searchTerm = null,
            string? category = null,
            bool lowStockOnly = false,
            bool expiringSoonOnly = false,
            int page = 1,
            int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetInventoryQuery(searchTerm, category, lowStockOnly, expiringSoonOnly, page, pageSize));
            return Results.Ok(result);
        })
        .WithName("GetInventory")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanDispenseMedication)
        .Produces<List<DrugDto>>(200);

        group.MapPost("/inventory", async (CreateDrugCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/api/v1/pharmacy/inventory/{result.Id}", result);
        })
        .WithName("CreateDrug")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanDispenseMedication)
        .Produces<DrugDto>(201)
        .Produces(400);

        group.MapPatch("/inventory/{id:guid}/stock", async (Guid id, UpdateStockRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateDrugStockCommand(id, request.NewQuantity));
            return Results.Ok(result);
        })
        .WithName("UpdateDrugStock")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanDispenseMedication)
        .Produces<DrugDto>(200)
        .Produces(404);

        group.MapGet("/prescriptions", async (
            IMediator mediator,
            Guid? patientId = null,
            string? status = null,
            int page = 1,
            int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetPrescriptionsQuery(patientId, status, page, pageSize));
            return Results.Ok(result);
        })
        .WithName("GetPrescriptions")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces<List<PrescriptionDto>>(200);

        group.MapPost("/prescriptions", async (CreatePrescriptionCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/api/v1/pharmacy/prescriptions/{result.Id}", result);
        })
        .WithName("CreatePrescription")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanPrescribeMedication)
        .Produces<PrescriptionDto>(201)
        .Produces(400);

        group.MapPost("/prescriptions/{id:guid}/dispense", async (Guid id, DispenseRequest? request, IMediator mediator) =>
        {
            var qty = request?.QuantityToDeduct ?? 1;
            var result = await mediator.Send(new DispensePrescriptionCommand(id, qty));
            return Results.Ok(result);
        })
        .WithName("DispensePrescription")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanDispenseMedication)
        .Produces<PrescriptionDto>(200)
        .Produces(400)
        .Produces(404)
        .Produces(409);
    }
}

public record UpdateStockRequest(int NewQuantity);
public record DispenseRequest(int QuantityToDeduct = 1);
