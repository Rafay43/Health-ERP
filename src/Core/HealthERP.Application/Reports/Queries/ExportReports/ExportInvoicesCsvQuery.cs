using MediatR;

namespace HealthERP.Application.Reports.Queries.ExportReports;

public record ExportInvoicesCsvQuery() : IRequest<byte[]>;
