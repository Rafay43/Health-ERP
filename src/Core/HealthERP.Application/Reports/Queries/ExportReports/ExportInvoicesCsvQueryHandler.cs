using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;
using System.Text;

namespace HealthERP.Application.Reports.Queries.ExportReports;

public class ExportInvoicesCsvQueryHandler : IRequestHandler<ExportInvoicesCsvQuery, byte[]>
{
    private readonly IApplicationDbContext _context;

    public ExportInvoicesCsvQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> Handle(ExportInvoicesCsvQuery request, CancellationToken cancellationToken)
    {
        var invoices = await _context.Invoices
            .Include(i => i.Patient)
            .AsNoTracking()
            .OrderByDescending(i => i.IssueDateUtc)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("InvoiceNumber,PatientName,IssueDateUtc,DueDateUtc,Status,TotalAmount,PaidAmount,BalanceDue");

        foreach (var i in invoices)
        {
            var pName = i.Patient != null ? $"{i.Patient.FirstName} {i.Patient.LastName}" : "Unknown";
            sb.AppendLine($"\"{i.InvoiceNumber}\",\"{pName}\",\"{i.IssueDateUtc:yyyy-MM-dd HH:mm:ss}\",\"{i.DueDateUtc:yyyy-MM-dd}\",\"{i.Status}\",{i.TotalAmount},{i.PaidAmount},{i.BalanceDue}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
