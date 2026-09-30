using MediatR;
using HealthERP.Application.Dashboard.Models;

namespace HealthERP.Application.Dashboard.Queries.GetDashboardMetrics;

public record GetDashboardMetricsQuery() : IRequest<DashboardMetricsDto>;
