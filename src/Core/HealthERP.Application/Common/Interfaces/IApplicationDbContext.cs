using Microsoft.EntityFrameworkCore;
using HealthERP.Domain.Patients;
using StaffEntity = HealthERP.Domain.StaffMembers.StaffMembers;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Billing;
using HealthERP.Domain.Pharmacy;
using HealthERP.Domain.Laboratory;
using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Triage;

namespace HealthERP.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Patient> Patients { get; }
    DbSet<StaffEntity> Staff { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Drug> Drugs { get; }
    DbSet<Prescription> Prescriptions { get; }
    DbSet<LabTest> LabTests { get; }
    DbSet<LabOrder> LabOrders { get; }
    DbSet<ClinicalNote> ClinicalNotes { get; }
    DbSet<Bed> Beds { get; }
    DbSet<Admission> Admissions { get; }
    DbSet<TriageRecord> TriageRecords { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}