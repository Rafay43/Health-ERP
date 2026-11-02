using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HealthERP.Domain.Patients;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new PatientId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.MedicalRecordNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(p => p.MedicalRecordNumber)
            .IsUnique();

        builder.Property(p => p.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.DateOfBirth)
            .IsRequired();

        builder.Property(p => p.Gender)
            .HasMaxLength(20);

        builder.Property(p => p.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(p => p.Email)
            .HasMaxLength(256);

        builder.Property(p => p.UserId)
            .IsRequired(false);

        builder.HasIndex(p => p.UserId)
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL");

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.CreatedBy)
            .HasMaxLength(100);

        builder.HasQueryFilter(p => !p.IsDeleted);

        // Domain events shouldn't be persisted as a column
        builder.Ignore(p => p.DomainEvents);
    }
}