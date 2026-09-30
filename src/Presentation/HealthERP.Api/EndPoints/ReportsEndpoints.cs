using MediatR;
using HealthERP.Application.Reports.Queries.GetExecutiveDashboardMetrics;
using HealthERP.Application.Reports.Queries.ExportReports;
using HealthERP.Application.Reports.Models;

namespace HealthERP.Api.Endpoints;

public static class ReportsEndpoints
{
    public static void MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        group.MapGet("/dashboard", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetExecutiveDashboardMetricsQuery());
            return Results.Ok(result);
        })
        .WithName("GetExecutiveDashboardMetrics")
        .Produces<ExecutiveDashboardMetricsDto>(200);

        group.MapGet("/export/invoices", async (IMediator mediator) =>
        {
            var csvBytes = await mediator.Send(new ExportInvoicesCsvQuery());
            return Results.File(csvBytes, "text/csv", "invoices-report.csv");
        })
        .WithName("ExportInvoicesCsv")
        .Produces(200, contentType: "text/csv");
    }
}
