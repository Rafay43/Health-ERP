using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HealthERP.Domain.Inpatient;
using HealthERP.Domain.Patients;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class BedConfiguration : IEntityTypeConfiguration<Bed>
{
    public void Configure(EntityTypeBuilder<Bed> builder)
    {
        builder.ToTable("beds", "inpatient");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasConversion(id => id.Value, value => new BedId(value))
            .ValueGeneratedNever();

        builder.Property(b => b.WardName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.RoomNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.BedNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.BedType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.DailyRate)
            .HasPrecision(18, 2);

        builder.HasIndex(b => new { b.WardName, b.RoomNumber, b.BedNumber })
            .IsUnique();

        builder.HasIndex(b => b.Status);
    }
}

public class AdmissionConfiguration : IEntityTypeConfiguration<Admission>
{
    public void Configure(EntityTypeBuilder<Admission> builder)
    {
        builder.ToTable("admissions", "inpatient");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AdmissionId(value))
            .ValueGeneratedNever();

        builder.Property(a => a.PatientId)
            .HasConversion(id => id.Value, value => new PatientId(value))
            .IsRequired();

        builder.Property(a => a.BedId)
            .HasConversion(id => id.Value, value => new BedId(value))
            .IsRequired();

        builder.Property(a => a.AdmittingDoctorName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.ChiefComplaint)
            .HasMaxLength(500);

        builder.Property(a => a.DischargeSummary)
            .HasMaxLength(2000);

        builder.Property(a => a.DailyRateSnapshot)
            .HasPrecision(18, 2);

        builder.Property(a => a.TotalRoomCharge)
            .HasPrecision(18, 2);

        builder.HasOne(a => a.Patient)
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Bed)
            .WithMany()
            .HasForeignKey(a => a.BedId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.IsDischarged);
    }
}
