using HealthERP.Application.PatientAccount.Commands.StartPatientActivation;
using HealthERP.Application.PatientAccount.Commands.VerifyPatientActivation;
using HealthERP.Application.PatientAccount.Models;
using HealthERP.Application.PatientAccount.Queries.GetPatientAccountStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace HealthERP.Api.Endpoints;

public static class PatientAccountEndpoints
{
    public static void MapPatientAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/patient-account")
            .WithTags("PatientAccount")
            .RequireAuthorization(new AuthorizeAttribute { Roles = HealthERP.Application.Common.Security.Roles.Patient });

        group.MapGet("/status", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPatientAccountStatusQuery());
            return Results.Ok(result);
        })
        .WithName("GetPatientAccountStatus")
        .Produces<PatientAccountStatusDto>(200);

        group.MapPost("/activation/start", async (StartPatientActivationRequest request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var command = new StartPatientActivationCommand(request.MedicalRecordNumber, request.DateOfBirth);
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("StartPatientActivation")
        .Produces<StartActivationResponseDto>(200)
        .Produces(409)
        .Produces(400);

        group.MapPost("/activation/verify", async (VerifyPatientActivationRequest request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var command = new VerifyPatientActivationCommand(request.Code);
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("VerifyPatientActivation")
        .Produces<VerifyActivationResponseDto>(200)
        .Produces(409)
        .Produces(400);
    }
}

public record StartPatientActivationRequest(
    string MedicalRecordNumber,
    DateOnly DateOfBirth);

public record VerifyPatientActivationRequest(
    string Code);
