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
            .WithTags("Patients");

        group.MapPost("/", async (RegisterPatientCommand command, IMediator mediator) =>
        {
            var patientId = await mediator.Send(command);
            return Results.Created($"/api/v1/patients/{patientId}", new { id = patientId });
        })
        .WithName("RegisterPatient")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanEditPatients)
        .Produces(201)
        .Produces(400);

        group.MapPut("/{id:guid}", async (Guid id, UpdatePatientRequest request, IMediator mediator) =>
        {
            var command = new HealthERP.Application.Patients.Commands.UpdatePatient.UpdatePatientCommand(
                id,
                request.FirstName,
                request.LastName,
                request.Gender,
                request.Email,
                request.PhoneNumber);
            await mediator.Send(command);
            return Results.NoContent();
        })
        .WithName("UpdatePatient")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanEditPatients)
        .Produces(204)
        .Produces(400)
        .Produces(404);

        group.MapGet("/", async (IMediator mediator, int page = 1, int pageSize = 25) =>
        {
            var patients = await mediator.Send(new GetPatientsQuery(page, pageSize));
            return Results.Ok(patients);
        })
        .WithName("GetPatients")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces<List<PatientDto>>(200);

        group.MapGet("/next-mrn", async (IMediator mediator) =>
        {
            var nextMrn = await mediator.Send(new GetNextMedicalRecordNumberQuery());
            return Results.Ok(new { medicalRecordNumber = nextMrn });
        })
        .WithName("GetNextMedicalRecordNumber")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanEditPatients)
        .Produces(200);

        group.MapGet("/{id:guid}/ehr", async (Guid id, IMediator mediator) =>
        {
            var ehr = await mediator.Send(new HealthERP.Application.Patients.Queries.GetPatientEhr.GetPatientEhrQuery(id));
            return Results.Ok(ehr);
        })
        .WithName("GetPatientEhr")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces<HealthERP.Application.Patients.Models.PatientEhrDto>(200)
        .Produces(404);

        group.MapPost("/{id:guid}/clinical-notes", async (Guid id, AddClinicalNoteRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new HealthERP.Application.Patients.Commands.AddClinicalNote.AddClinicalNoteCommand(
                id,
                request.DoctorName,
                request.ChiefComplaint,
                request.Diagnosis,
                request.Notes,
                request.BloodPressure,
                request.HeartRateBpm,
                request.TemperatureCelsius,
                request.WeightKg));
            return Results.Created($"/api/v1/patients/{id}/clinical-notes/{result.Id}", result);
        })
        .WithName("AddClinicalNote")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanEditPatients)
        .Produces<HealthERP.Application.Patients.Models.ClinicalNoteDto>(201)
        .Produces(400)
        .Produces(404);
    }
}

public record AddClinicalNoteRequest(
    string DoctorName,
    string ChiefComplaint,
    string Diagnosis,
    string Notes,
    string? BloodPressure = null,
    int? HeartRateBpm = null,
    decimal? TemperatureCelsius = null,
    decimal? WeightKg = null
);

public record UpdatePatientRequest(
    string FirstName,
    string LastName,
    string? Gender = null,
    string? Email = null,
    string? PhoneNumber = null);