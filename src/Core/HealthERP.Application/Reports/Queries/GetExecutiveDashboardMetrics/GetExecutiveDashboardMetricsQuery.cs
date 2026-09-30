using MediatR;
using HealthERP.Application.Reports.Models;

namespace HealthERP.Application.Reports.Queries.GetExecutiveDashboardMetrics;

public record GetExecutiveDashboardMetricsQuery() : IRequest<ExecutiveDashboardMetricsDto>;
