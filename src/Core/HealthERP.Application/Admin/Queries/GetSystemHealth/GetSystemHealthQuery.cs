using MediatR;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Application.Admin.Queries.GetSystemHealth;

public record GetSystemHealthQuery() : IRequest<SystemHealthDto>;
