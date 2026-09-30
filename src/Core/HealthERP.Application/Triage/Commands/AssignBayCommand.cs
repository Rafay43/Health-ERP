using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Triage;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Triage.Commands;

public record AssignBayCommand(
    Guid TriageId,
    string BayName,
    string AttendingPhysicianName
) : IRequest<bool>;

public class AssignBayCommandHandler : IRequestHandler<AssignBayCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public AssignBayCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(AssignBayCommand request, CancellationToken cancellationToken)
    {
        var triageId = new TriageId(request.TriageId);

        var record = await _context.TriageRecords.FirstOrDefaultAsync(t => t.Id == triageId, cancellationToken);
        if (record == null)
        {
            throw new KeyNotFoundException($"Emergency Triage record '{request.TriageId}' was not found.");
        }

        record.AssignBay(request.BayName, request.AttendingPhysicianName);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
