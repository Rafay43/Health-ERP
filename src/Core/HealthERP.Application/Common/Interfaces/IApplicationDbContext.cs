using Microsoft.EntityFrameworkCore;
using HealthERP.Domain.Patients;

namespace HealthERP.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Patient> Patients { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}