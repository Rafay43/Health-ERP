using HealthERP.Application.Triage.Commands;
using HealthERP.Application.Triage.Models;
using HealthERP.Application.Triage.Queries;
using MediatR;

namespace HealthERP.Api.Endpoints;

public static class EmergencyEndpoints
{
    public static void MapEmergencyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/emergency")
            .WithTags("Emergency")
            .RequireAuthorization();

        group.MapGet("/queue", async (IMediator mediator, bool activeOnly = true) =>
        {
            var result = await mediator.Send(new GetEmergencyQueueQuery(activeOnly));
            return Results.Ok(result);
        })
        .WithName("GetEmergencyQueue")
        .Produces<List<TriageRecordDto>>(200);

        group.MapPost("/triage", async (CreateTriageRecordCommand command, IMediator mediator) =>
        {
            var id = await mediator.Send(command);
            return Results.Created($"/api/v1/emergency/triage/{id}", new { id });
        })
        .WithName("CreateTriageRecord")
        .Produces<object>(201)
        .Produces(400);

        group.MapPost("/triage/{id:guid}/assign-bay", async (Guid id, AssignBayRequest request, IMediator mediator) =>
        {
            var success = await mediator.Send(new AssignBayCommand(id, request.BayName, request.AttendingPhysicianName));
            return Results.Ok(new { success });
        })
        .WithName("AssignEmergencyBay")
        .Produces<object>(200)
        .Produces(400);

        group.MapPost("/triage/{id:guid}/transfer-inpatient", async (Guid id, TransferInpatientRequest request, IMediator mediator) =>
        {
            var admissionId = await mediator.Send(new TransferToInpatientCommand(
                id,
                request.BedId,
                request.AdmittingDoctorName,
                request.AdmissionNotes));
            return Results.Ok(new { admissionId });
        })
        .WithName("TransferToInpatient")
        .Produces<object>(200)
        .Produces(400);

        group.MapPost("/triage/{id:guid}/discharge", async (Guid id, IMediator mediator) =>
        {
            var success = await mediator.Send(new DischargeFromEDCommand(id));
            return Results.Ok(new { success });
        })
        .WithName("DischargeFromEmergency")
        .Produces<object>(200)
        .Produces(400);
    }
}

public record AssignBayRequest(string BayName, string AttendingPhysicianName);
public record TransferInpatientRequest(Guid BedId, string AdmittingDoctorName, string AdmissionNotes);
