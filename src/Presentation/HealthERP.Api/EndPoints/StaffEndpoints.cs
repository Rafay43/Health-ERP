using MediatR;
using HealthERP.Application.StaffMembers.Commands.CreateStaff;
using HealthERP.Application.StaffMembers.Queries.GetStaff;
using HealthERP.Application.StaffMembers.Models;

namespace HealthERP.Api.Endpoints;

public static class StaffEndpoints
{
    public static void MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/staff")
            .WithTags("Staff")
            .RequireAuthorization();

        group.MapPost("/", async (CreateStaffCommand command, IMediator mediator) =>
        {
            var staffId = await mediator.Send(command);
            return Results.Created($"/api/v1/staff/{staffId}", new { id = staffId });
        })
        .WithName("CreateStaff")
        .Produces(201)
        .Produces(400);

        group.MapGet("/", async (IMediator mediator) =>
        {
            var staffList = await mediator.Send(new GetStaffQuery());
            return Results.Ok(staffList);
        })
        .WithName("GetStaff")
        .Produces<List<StaffDto>>(200);
    }
}
