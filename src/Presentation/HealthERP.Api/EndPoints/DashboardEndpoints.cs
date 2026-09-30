using MediatR;
using HealthERP.Application.Dashboard.Queries.GetDashboardMetrics;

namespace HealthERP.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();

        group.MapGet("/metrics", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetDashboardMetricsQuery();
            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetDashboardMetrics")
        .Produces<HealthERP.Application.Dashboard.Models.DashboardMetricsDto>(StatusCodes.Status200OK);
    }
}
