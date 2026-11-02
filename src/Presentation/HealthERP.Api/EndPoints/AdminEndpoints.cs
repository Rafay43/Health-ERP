using MediatR;
using HealthERP.Application.Admin.Queries.GetUserAccounts;
using HealthERP.Application.Admin.Queries.GetAuditLogs;
using HealthERP.Application.Admin.Queries.GetSystemHealth;
using HealthERP.Application.Admin.Commands.UpdateUserRole;
using HealthERP.Application.Admin.Commands.ToggleUserAccountStatus;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization(HealthERP.Application.Common.Security.Policies.CanManageStaff);

        group.MapGet("/users", async (IMediator mediator) =>
        {
            var users = await mediator.Send(new GetUserAccountsQuery());
            return Results.Ok(users);
        })
        .WithName("GetUserAccounts")
        .Produces<List<UserAccountDto>>(200);

        group.MapPut("/users/{id:guid}/role", async (Guid id, UpdateRoleRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateUserRoleCommand(id, request.RoleName));
            return Results.Ok(result);
        })
        .WithName("UpdateUserRole")
        .Produces<UserAccountDto>(200)
        .Produces(404);

        group.MapPatch("/users/{id:guid}/status", async (Guid id, ToggleStatusRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new ToggleUserAccountStatusCommand(id, request.EnableAccount));
            return Results.Ok(result);
        })
        .WithName("ToggleUserAccountStatus")
        .Produces<UserAccountDto>(200)
        .Produces(404);

        group.MapGet("/audit-logs", async (IMediator mediator, string? entityName = null, int page = 1, int pageSize = 50) =>
        {
            var logs = await mediator.Send(new GetAuditLogsQuery(entityName, page, pageSize));
            return Results.Ok(logs);
        })
        .WithName("GetAuditLogs")
        .Produces<List<AuditLogEntryDto>>(200);

        group.MapGet("/health", async (IMediator mediator) =>
        {
            var health = await mediator.Send(new GetSystemHealthQuery());
            return Results.Ok(health);
        })
        .WithName("GetSystemHealth")
        .Produces<SystemHealthDto>(200);
    }
}

public record UpdateRoleRequest(string RoleName);
public record ToggleStatusRequest(bool EnableAccount);
