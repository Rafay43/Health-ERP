using MediatR;
using HealthERP.Application.StaffMembers.Commands.CreateStaff;
using HealthERP.Application.StaffMembers.Queries.GetStaff;
using HealthERP.Application.StaffMembers.Models;
using HealthERP.Application.StaffMembers.Commands.ManageStaffAccount;
using HealthERP.Application.StaffMembers.Queries.LookupStaffAccounts;
using HealthERP.Domain.StaffMembers;

namespace HealthERP.Api.Endpoints;

public static class StaffEndpoints
{
    public static void MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/staff")
            .WithTags("Staff");

        group.MapGet("/account-lookup", async (IMediator mediator, CancellationToken ct, string? search = null, int page = 1, int pageSize = 20) =>
            Results.Ok(await mediator.Send(new LookupStaffAccountsQuery(search, page, pageSize), ct)))
            .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanManageStaff)
            .Produces<StaffAccountLookupDto>(200).Produces(400);

        group.MapPost("/{id:guid}/account-link", async (Guid id, LinkStaffAccountRequest request, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new LinkStaffAccountCommand(id, request.UserId, request.StaffKind ?? throw new ArgumentException("Staff classification is required.")), ct)))
            .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanManageStaff)
            .Produces<StaffAccountLinkDto>(200).Produces(400).Produces(404).Produces(409);

        group.MapDelete("/{id:guid}/account-link", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new UnlinkStaffAccountCommand(id), ct); return Results.NoContent();
        }).RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanManageStaff)
            .Produces(204).Produces(404).Produces(409);

        group.MapPost("/", async (CreateStaffCommand command, IMediator mediator) =>
        {
            var staffId = await mediator.Send(command);
            return Results.Created($"/api/v1/staff/{staffId}", new { id = staffId });
        })
        .WithName("CreateStaff")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanManageStaff)
        .Produces(201)
        .Produces(400);

        group.MapGet("/", async (IMediator mediator) =>
        {
            var staffList = await mediator.Send(new GetStaffQuery());
            return Results.Ok(staffList);
        })
        .WithName("GetStaff")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanManageStaff)
        .Produces<List<StaffDto>>(200);

        group.MapGet("/lookup/doctors", async (IMediator mediator) =>
        {
            var doctors = await mediator.Send(new HealthERP.Application.StaffMembers.Queries.GetDoctorsLookup.GetDoctorsLookupQuery());
            return Results.Ok(doctors);
        })
        .WithName("GetDoctorsLookup")
        .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanViewPatients)
        .Produces<List<HealthERP.Application.StaffMembers.Queries.GetDoctorsLookup.DoctorLookupDto>>(200);
    }
}

public record LinkStaffAccountRequest(Guid UserId, StaffKind? StaffKind);
