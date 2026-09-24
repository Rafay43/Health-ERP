using MediatR;
using Microsoft.EntityFrameworkCore;
using HealthERP.Application.Common.Interfaces;

namespace HealthERP.Application.Patients.Queries.GetNextMrn;

public class GetNextMedicalRecordNumberQueryHandler
    : IRequestHandler<GetNextMedicalRecordNumberQuery, string>
{
    private readonly IApplicationDbContext _context;

    public GetNextMedicalRecordNumberQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> Handle(GetNextMedicalRecordNumberQuery request, CancellationToken cancellationToken)
    {
        var count = await _context.Patients.CountAsync(cancellationToken);
        var next = count + 1;
        return $"MRN-{next:D4}";
    }
}