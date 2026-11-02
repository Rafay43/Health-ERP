using MediatR;
using HealthERP.Application.Appointments.Commands.BookAppointment;
using HealthERP.Application.Appointments.Commands.UpdateAppointmentStatus;
using HealthERP.Application.Appointments.Queries.GetAppointments;
using HealthERP.Application.Appointments.Models;

namespace HealthERP.Api.Endpoints;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/appointments")
            .WithTags("Appointments");

        group.MapPost("/", async (BookAppointmentCommand command, IMediator mediator) =>
        {
            var appointmentId = await mediator.Send(command);
            return Results.Created($"/api/v1/appointments/{appointmentId}", new { id = appointmentId });
        })
        .WithName("BookAppointment")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces(201)
        .Produces(400);

        group.MapGet("/", async (IMediator mediator) =>
        {
            var appointments = await mediator.Send(new GetAppointmentsQuery());
            return Results.Ok(appointments);
        })
        .WithName("GetAppointments")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces<List<AppointmentDto>>(200);

        group.MapPatch("/{id:guid}/status", async (Guid id, UpdateAppointmentStatusRequest request, IMediator mediator) =>
        {
            await mediator.Send(new UpdateAppointmentStatusCommand(id, request.Status));
            return Results.NoContent();
        })
        .WithName("UpdateAppointmentStatus")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces(204)
        .Produces(400)
        .Produces(404);
    }
}

public record UpdateAppointmentStatusRequest(HealthERP.Domain.Appointments.AppointmentStatus Status);
