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
            .WithTags("Inpatient")
            .RequireAuthorization();

        group.MapGet("/beds", async (IMediator mediator, string? wardName = null) =>
        {
            var result = await mediator.Send(new GetBedGridQuery(wardName));
            return Results.Ok(result);
        })
        .WithName("GetBedGrid")
        .Produces<List<BedDto>>(200);

        group.MapPost("/beds", async (CreateBedCommand command, IMediator mediator) =>
        {
            var id = await mediator.Send(command);
            return Results.Created($"/api/v1/inpatient/beds/{id}", new { id });
        })
        .WithName("CreateBed")
        .Produces<object>(201)
        .Produces(400);

        group.MapGet("/admissions", async (IMediator mediator, Guid? patientId = null, bool? activeOnly = null) =>
        {
            var result = await mediator.Send(new GetAdmissionsQuery(patientId, activeOnly));
            return Results.Ok(result);
        })
        .WithName("GetAdmissions")
        .Produces<List<AdmissionDto>>(200);

        group.MapPost("/admissions", async (AdmitPatientCommand command, IMediator mediator) =>
        {
            var id = await mediator.Send(command);
            return Results.Created($"/api/v1/inpatient/admissions/{id}", new { id });
        })
        .WithName("AdmitPatient")
        .Produces<object>(201)
        .Produces(400);

        group.MapPost("/admissions/{id:guid}/discharge", async (Guid id, DischargeRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new DischargePatientCommand(id, request.DischargeSummary));
            return Results.Ok(result);
        })
        .WithName("DischargePatient")
        .Produces<AdmissionDto>(200)
        .Produces(400)
        .Produces(404);
    }
}

public record DischargeRequest(string DischargeSummary);
