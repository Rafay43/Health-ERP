using Microsoft.EntityFrameworkCore;
using HealthERP.Domain.Patients;
using StaffEntity = HealthERP.Domain.StaffMembers.StaffMembers;
using HealthERP.Domain.Appointments;

namespace HealthERP.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Patient> Patients { get; }
    DbSet<StaffEntity> Staff { get; }
     DbSet<Appointment> Appointments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}