using HealthERP.Application.Common.Interfaces;
using HealthERP.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace HealthERP.Infrastructure.Services;

public class CurrentPatientService : ICurrentPatientService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CurrentPatientService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Patient?> GetCurrentPatientAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return null;
        }

        return await _context.Patients
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<bool> IsCurrentPatientLinkedAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return false;
        }

        return await _context.Patients
            .AnyAsync(p => p.UserId == userId, cancellationToken);
    }
}
