using MediatR;
using HealthERP.Application.Patients.Commands.RegisterPatient;
using HealthERP.Application.Patients.Queries.GetPatients;
using HealthERP.Application.Patients.Queries.GetNextMrn;

namespace HealthERP.Api.Endpoints;

public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/patients")
            .WithTags("Patients")
            .RequireAuthorization();

        group.MapPost("/", async (RegisterPatientCommand command, IMediator mediator) =>
        {
            var patientId = await mediator.Send(command);
            return Results.Created($"/api/v1/patients/{patientId}", new { id = patientId });
        })
        .WithName("RegisterPatient")
        .Produces(201)
        .Produces(400);

        group.MapGet("/", async (IMediator mediator, int page = 1, int pageSize = 25) =>
        {
            var patients = await mediator.Send(new GetPatientsQuery(page, pageSize));
            return Results.Ok(patients);
        })
        .WithName("GetPatients")
        .Produces<List<PatientDto>>(200);

        group.MapGet("/next-mrn", async (IMediator mediator) =>
        {
            var nextMrn = await mediator.Send(new GetNextMedicalRecordNumberQuery());
            return Results.Ok(new { medicalRecordNumber = nextMrn });
        })
        .WithName("GetNextMedicalRecordNumber")
        .Produces(200);
    }
}