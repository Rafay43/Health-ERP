using MediatR;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Application.Admin.Queries.GetAuditLogs;

public record GetAuditLogsQuery(
    string? EntityName = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<List<AuditLogEntryDto>>;
