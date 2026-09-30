using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HealthERP.Domain.Pharmacy;
using HealthERP.Domain.Patients;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class DrugConfiguration : IEntityTypeConfiguration<Drug>
{
    public void Configure(EntityTypeBuilder<Drug> builder)
    {
        builder.ToTable("drugs", "pharmacy");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new DrugId(value))
            .ValueGeneratedNever();

        builder.Property(d => d.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.GenericName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.UnitPrice)
            .HasPrecision(18, 2);

        builder.HasIndex(d => d.Name);
        builder.HasIndex(d => d.Category);
    }
}

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("prescriptions", "pharmacy");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new PrescriptionId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.PatientId)
            .HasConversion(id => id.Value, value => new PatientId(value))
            .IsRequired();

        builder.Property(p => p.DrugId)
            .HasConversion(
                (DrugId? id) => id.HasValue ? id.Value.Value : (Guid?)null,
                (Guid? value) => value.HasValue ? new DrugId(value.Value) : (DrugId?)null);

        builder.Property(p => p.DoctorName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.MedicationName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Dosage)
            .HasMaxLength(100);

        builder.Property(p => p.Frequency)
            .HasMaxLength(100);

        builder.Property(p => p.Instructions)
            .HasMaxLength(500);

        builder.HasOne(p => p.Patient)
            .WithMany()
            .HasForeignKey(p => p.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Drug)
            .WithMany()
            .HasForeignKey(p => p.DrugId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.Status);
    }
}
