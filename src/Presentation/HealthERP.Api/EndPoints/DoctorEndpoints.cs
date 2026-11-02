using HealthERP.Application.Common.Security;
using HealthERP.Application.DoctorWorkspace.Models;
using HealthERP.Application.DoctorWorkspace.Queries;
using MediatR;
using HealthERP.Application.DoctorWorkspace.Commands;

namespace HealthERP.Api.Endpoints;

public static class DoctorEndpoints
{
    public static void MapDoctorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/doctor/schedule", async (DateOnly? date, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetDoctorScheduleQuery(date), ct)))
            .WithTags("Doctor Workspace").WithName("GetDoctorSchedule")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Doctor))
            .Produces<DoctorScheduleDto>().Produces(400).Produces(401).Produces(403);

        app.MapGet("/api/v1/doctor/patients/{patientId}/snapshot", async (Guid patientId, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetDoctorPatientSnapshotQuery(patientId), ct)))
            .WithTags("Doctor Workspace").WithName("GetDoctorPatientSnapshot")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Doctor))
            .Produces<DoctorPatientSnapshotDto>().Produces(400).Produces(401).Produces(403).Produces(404);

        app.MapPost("/api/v1/doctor/patients/{patientId}/clinical-notes", async (Guid patientId, DoctorClinicalNoteInput input, IMediator mediator, CancellationToken ct) =>
        {
            var note = await mediator.Send(new RecordDoctorClinicalNoteCommand(patientId, input), ct);
            return Results.Created($"/api/v1/doctor/patients/{patientId}/snapshot", note);
        }).WithTags("Doctor Workspace").WithName("RecordDoctorClinicalNote")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Doctor))
            .Produces<RecordedClinicalNoteDto>(201).Produces(400).Produces(401).Produces(403).Produces(404);
    }
}
