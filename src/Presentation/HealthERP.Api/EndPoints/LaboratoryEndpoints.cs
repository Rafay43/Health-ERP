using MediatR;
using HealthERP.Application.Laboratory.Queries.GetLabCatalog;
using HealthERP.Application.Laboratory.Queries.GetLabOrders;
using HealthERP.Application.Laboratory.Commands.CreateLabTest;
using HealthERP.Application.Laboratory.Commands.CreateLabOrder;
using HealthERP.Application.Laboratory.Commands.RecordLabResult;
using HealthERP.Application.Laboratory.Models;

namespace HealthERP.Api.Endpoints;

public static class LaboratoryEndpoints
{
    public static void MapLaboratoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/laboratory")
            .WithTags("Laboratory");

        group.MapGet("/catalog", async (
            IMediator mediator,
            string? searchTerm = null,
            string? category = null,
            bool activeOnly = true,
            int page = 1,
            int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetLabCatalogQuery(searchTerm, category, activeOnly, page, pageSize));
            return Results.Ok(result);
        })
        .WithName("GetLabCatalog")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces<List<LabTestDto>>(200);

        group.MapPost("/catalog", async (CreateLabTestCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/api/v1/laboratory/catalog/{result.Id}", result);
        })
        .WithName("CreateLabTest")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanEnterLabResult)
        .Produces<LabTestDto>(201)
        .Produces(400);

        group.MapGet("/orders", async (
            IMediator mediator,
            Guid? patientId = null,
            string? status = null,
            string? priority = null,
            bool abnormalOnly = false,
            int page = 1,
            int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetLabOrdersQuery(patientId, status, priority, abnormalOnly, page, pageSize));
            return Results.Ok(result);
        })
        .WithName("GetLabOrders")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces<List<LabOrderDto>>(200);

        group.MapPost("/orders", async (CreateLabOrderCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/api/v1/laboratory/orders/{result.Id}", result);
        })
        .WithName("CreateLabOrder")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanOrderLabTest)
        .Produces<LabOrderDto>(201)
        .Produces(400);

        group.MapPost("/orders/{id:guid}/results", async (Guid id, RecordResultRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new RecordLabResultCommand(
                id,
                request.ResultValue,
                request.Remarks,
                request.PerformedBy,
                request.IsAbnormal));
            return Results.Ok(result);
        })
        .WithName("RecordLabResult")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanEnterLabResult)
        .Produces<LabOrderDto>(200)
        .Produces(400)
        .Produces(404);
    }
}

public record RecordResultRequest(
    string ResultValue,
    string? Remarks,
    string PerformedBy,
    bool IsAbnormal = false
);
