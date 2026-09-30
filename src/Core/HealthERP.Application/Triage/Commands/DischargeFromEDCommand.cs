using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Triage;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Triage.Commands;

public record DischargeFromEDCommand(
    Guid TriageId
) : IRequest<bool>;

public class DischargeFromEDCommandHandler : IRequestHandler<DischargeFromEDCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DischargeFromEDCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DischargeFromEDCommand request, CancellationToken cancellationToken)
    {
        var triageId = new TriageId(request.TriageId);

        var triageRecord = await _context.TriageRecords.FirstOrDefaultAsync(t => t.Id == triageId, cancellationToken);
        if (triageRecord == null)
        {
            throw new KeyNotFoundException($"Triage record '{request.TriageId}' was not found.");
        }

        triageRecord.DischargeFromED();
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
