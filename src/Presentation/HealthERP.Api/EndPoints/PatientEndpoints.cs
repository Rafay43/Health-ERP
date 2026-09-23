using MediatR;
using HealthERP.Application.Patients.Commands.RegisterPatient;

namespace HealthERP.Api.Endpoints;

public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/patients")
            .WithTags("Patients");

        group.MapPost("/", async (RegisterPatientCommand command, IMediator mediator) =>
        {
            var patientId = await mediator.Send(command);
            return Results.Created($"/api/v1/patients/{patientId}", new { id = patientId });
        })
        .WithName("RegisterPatient")
        .Produces(201)
        .Produces(400);
    }
}