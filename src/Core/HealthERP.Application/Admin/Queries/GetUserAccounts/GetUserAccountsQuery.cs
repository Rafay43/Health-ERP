using MediatR;
using HealthERP.Application.Admin.Models;

namespace HealthERP.Application.Admin.Queries.GetUserAccounts;

public record GetUserAccountsQuery() : IRequest<List<UserAccountDto>>;
