using MediatR;
using HealthERP.Application.Laboratory.Models;

namespace HealthERP.Application.Laboratory.Queries.GetLabCatalog;

public record GetLabCatalogQuery(
    string? SearchTerm = null,
    string? Category = null,
    bool ActiveOnly = true,
    int Page = 1,
    int PageSize = 50
) : IRequest<List<LabTestDto>>;
