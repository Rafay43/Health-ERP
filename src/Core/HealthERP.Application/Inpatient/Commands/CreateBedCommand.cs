using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Inpatient;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Application.Inpatient.Commands;

public record CreateBedCommand(
    string WardName,
    string RoomNumber,
    string BedNumber,
    string BedType,
    decimal DailyRate
) : IRequest<Guid>;

public class CreateBedCommandHandler : IRequestHandler<CreateBedCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateBedCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateBedCommand request, CancellationToken cancellationToken)
    {
        var existing = await _context.Beds.AnyAsync(b =>
            b.WardName.ToLower() == request.WardName.ToLower().Trim() &&
            b.RoomNumber.ToLower() == request.RoomNumber.ToLower().Trim() &&
            b.BedNumber.ToLower() == request.BedNumber.ToLower().Trim(), cancellationToken);

        if (existing)
        {
            throw new InvalidOperationException($"Bed '{request.BedNumber}' in Room '{request.RoomNumber}', Ward '{request.WardName}' already exists.");
        }

        var bed = Bed.Create(
            request.WardName,
            request.RoomNumber,
            request.BedNumber,
            request.BedType,
            request.DailyRate
        );

        _context.Beds.Add(bed);
        await _context.SaveChangesAsync(cancellationToken);

        return bed.Id.Value;
    }
}
