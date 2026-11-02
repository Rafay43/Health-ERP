using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HealthERP.Domain.Patients;
using HealthERP.Domain.StaffMembers;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class ClinicalNoteConfiguration : IEntityTypeConfiguration<ClinicalNote>
{
    public void Configure(EntityTypeBuilder<ClinicalNote> builder)
    {
        builder.ToTable("clinical_notes", "patients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new ClinicalNoteId(value))
            .ValueGeneratedNever();

        builder.Property(c => c.PatientId)
            .HasConversion(id => id.Value, value => new PatientId(value))
            .IsRequired();

        builder.Property(c => c.DoctorName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.ChiefComplaint)
            .HasMaxLength(500);

        builder.Property(c => c.Diagnosis)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(c => c.Notes)
            .HasMaxLength(2000);

        builder.Property(c => c.BloodPressure)
            .HasMaxLength(50);

        builder.Property(c => c.TemperatureCelsius)
            .HasPrecision(5, 2);

        builder.Property(c => c.WeightKg)
            .HasPrecision(5, 2);

        builder.HasOne(c => c.Patient)
            .WithMany()
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.NoteDateUtc);
        builder.Property(c => c.RecordedByStaffId).HasConversion(
            (StaffId? id) => id.HasValue ? id.Value.Value : (Guid?)null,
            (Guid? value) => value.HasValue ? new StaffId(value.Value) : (StaffId?)null);
        builder.HasOne(c => c.RecordedByStaff).WithMany().HasForeignKey(c => c.RecordedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
