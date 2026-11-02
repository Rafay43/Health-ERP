using HealthERP.Application.PatientPortal.Models;
using HealthERP.Application.PatientPortal.Queries.GetMyBilling;
using HealthERP.Application.PatientPortal.Queries.GetMyDashboard;
using HealthERP.Application.PatientPortal.Queries.GetMyHealthRecord;
using HealthERP.Application.PatientPortal.Queries.GetMyLabResults;
using HealthERP.Application.PatientPortal.Queries.GetMyAppointments;
using HealthERP.Application.PatientPortal.Queries.GetMyPrescriptions;
using HealthERP.Application.PatientPortal.Queries.GetMyProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace HealthERP.Api.Endpoints;

public static class PatientPortalEndpoints
{
    public static void MapPatientPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/patient-portal")
            .WithTags("PatientPortal")
            .RequireAuthorization(new AuthorizeAttribute { Roles = HealthERP.Application.Common.Security.Roles.Patient });

        group.MapGet("/me", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMyProfileQuery());
            return Results.Ok(result);
        })
        .WithName("GetMyPatientProfile")
        .Produces<PatientPortalProfileDto>(200)
        .Produces(401)
        .Produces(403);

        group.MapGet("/me/dashboard", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMyDashboardQuery());
            return Results.Ok(result);
        })
        .WithName("GetMyPatientDashboard")
        .Produces<PatientDashboardDto>(200)
        .Produces(401)
        .Produces(403);

        group.MapGet("/me/appointments", async (IMediator mediator, int page = 1, int pageSize = 25, string? status = null) =>
        {
            var result = await mediator.Send(new GetMyAppointmentsQuery(page, pageSize, status));
            return Results.Ok(result);
        })
        .WithName("GetMyPatientAppointments")
        .Produces<List<PatientAppointmentDto>>(200)
        .Produces(401)
        .Produces(403);

        group.MapGet("/me/health-record", async (IMediator mediator, int page = 1, int pageSize = 25) =>
        {
            var result = await mediator.Send(new GetMyHealthRecordQuery(page, pageSize));
            return Results.Ok(result);
        })
        .WithName("GetMyPatientHealthRecord")
        .Produces<PatientHealthRecordDto>(200)
        .Produces(401)
        .Produces(403);

        group.MapGet("/me/prescriptions", async (IMediator mediator, int page = 1, int pageSize = 25, string? status = null) =>
        {
            var result = await mediator.Send(new GetMyPrescriptionsQuery(page, pageSize, status));
            return Results.Ok(result);
        })
        .WithName("GetMyPatientPrescriptions")
        .Produces<List<PatientPrescriptionDto>>(200)
        .Produces(401)
        .Produces(403);

        group.MapGet("/me/lab-results", async (IMediator mediator, int page = 1, int pageSize = 25) =>
        {
            var result = await mediator.Send(new GetMyLabResultsQuery(page, pageSize, CompletedOnly: true));
            return Results.Ok(result);
        })
        .WithName("GetMyPatientLabResults")
        .Produces<List<PatientLabResultDto>>(200)
        .Produces(401)
        .Produces(403);

        group.MapGet("/me/billing", async (IMediator mediator, int page = 1, int pageSize = 25, string? status = null) =>
        {
            var result = await mediator.Send(new GetMyBillingQuery(page, pageSize, status));
            return Results.Ok(result);
        })
        .WithName("GetMyPatientBilling")
        .Produces<List<PatientInvoiceDto>>(200)
        .Produces(401)
        .Produces(403);
    }
}
