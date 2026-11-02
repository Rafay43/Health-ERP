using HealthERP.Application.Inpatient.Commands;
using HealthERP.Application.Inpatient.Models;
using HealthERP.Application.Inpatient.Queries;
using MediatR;

namespace HealthERP.Api.Endpoints;

public static class InpatientEndpoints
{
    public static void MapInpatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/inpatient")
            .WithTags("Inpatient");

        group.MapGet("/beds", async (IMediator mediator, string? wardName = null) =>
        {
            var result = await mediator.Send(new GetBedGridQuery(wardName));
            return Results.Ok(result);
        })
        .WithName("GetBedGrid")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces<List<BedDto>>(200);

        group.MapPost("/beds", async (CreateBedCommand command, IMediator mediator) =>
        {
            var id = await mediator.Send(command);
            return Results.Created($"/api/v1/inpatient/beds/{id}", new { id });
        })
        .WithName("CreateBed")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanManageStaff)
        .Produces<object>(201)
        .Produces(400);

        group.MapGet("/admissions", async (IMediator mediator, Guid? patientId = null, bool? activeOnly = null) =>
        {
            var result = await mediator.Send(new GetAdmissionsQuery(patientId, activeOnly));
            return Results.Ok(result);
        })
        .WithName("GetAdmissions")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces<List<AdmissionDto>>(200);

        group.MapPost("/admissions", async (AdmitPatientCommand command, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var id = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/v1/inpatient/admissions/{id}", new { id });
        })
        .WithName("AdmitPatient")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanManageAdmissions)
        .Produces<object>(201)
        .Produces(400)
        .Produces(404)
        .Produces(409);

        group.MapPost("/admissions/{id:guid}/discharge", async (Guid id, DischargeRequest request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new DischargePatientCommand(id, request.DischargeSummary), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("DischargePatient")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanEditPatients)
        .Produces<AdmissionDto>(200)
        .Produces(400)
        .Produces(409)
        .Produces(404);

        group.MapPost("/admissions/{id:guid}/transfer", async (Guid id, TransferBedRequest request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new TransferBedCommand(id, request.NewBedId, request.TransferReason), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("TransferBed")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanManageAdmissions)
        .Produces<AdmissionDto>(200)
        .Produces(400)
        .Produces(409)
        .Produces(404);
    }
}

public record DischargeRequest(string DischargeSummary);
public record TransferBedRequest(Guid NewBedId, string? TransferReason);

