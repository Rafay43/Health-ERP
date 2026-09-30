using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Triage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class TriageConfiguration : IEntityTypeConfiguration<TriageRecord>
{
    public void Configure(EntityTypeBuilder<TriageRecord> builder)
    {
        builder.ToTable("triage_records", "triage");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(
                id => id.Value,
                value => new TriageId(value));

        builder.Property(t => t.PatientId)
            .HasConversion(
                id => id.Value,
                value => new PatientId(value))
            .IsRequired();

        builder.Property(t => t.TransferredBedId)
            .HasConversion(
                (BedId? id) => id.HasValue ? id.Value.Value : (Guid?)null,
                (Guid? value) => value.HasValue ? new BedId(value.Value) : (BedId?)null);

        builder.Property(t => t.ChiefComplaint)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.BloodPressure)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.TriageNurseName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.AttendingPhysicianName)
            .HasMaxLength(200);

        builder.Property(t => t.AssignedBay)
            .HasMaxLength(100);

        builder.HasOne(t => t.Patient)
            .WithMany()
            .HasForeignKey(t => t.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.PatientId);
        builder.HasIndex(t => t.Severity);
        builder.HasIndex(t => t.Status);
    }
}
