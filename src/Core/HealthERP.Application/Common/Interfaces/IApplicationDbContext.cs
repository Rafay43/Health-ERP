using Microsoft.EntityFrameworkCore;
using HealthERP.Domain.Patients;
using StaffEntity = HealthERP.Domain.StaffMembers.StaffMembers;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Billing;

namespace HealthERP.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Patient> Patients { get; }
    DbSet<StaffEntity> Staff { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<Invoice> Invoices { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}