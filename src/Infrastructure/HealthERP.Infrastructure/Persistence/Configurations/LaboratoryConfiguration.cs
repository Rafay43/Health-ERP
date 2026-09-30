using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HealthERP.Domain.Laboratory;
using HealthERP.Domain.Patients;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class LabTestConfiguration : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> builder)
    {
        builder.ToTable("lab_tests", "laboratory");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new LabTestId(value))
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(t => t.Code)
            .IsUnique();

        builder.Property(t => t.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.StandardPrice)
            .HasPrecision(18, 2);

        builder.Property(t => t.SampleType)
            .HasMaxLength(100);

        builder.Property(t => t.ReferenceRange)
            .HasMaxLength(200);

        builder.Property(t => t.Unit)
            .HasMaxLength(50);
    }
}

public class LabOrderConfiguration : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> builder)
    {
        builder.ToTable("lab_orders", "laboratory");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasConversion(id => id.Value, value => new LabOrderId(value))
            .ValueGeneratedNever();

        builder.Property(o => o.PatientId)
            .HasConversion(id => id.Value, value => new PatientId(value))
            .IsRequired();

        builder.Property(o => o.LabTestId)
            .HasConversion(
                (LabTestId? id) => id.HasValue ? id.Value.Value : (Guid?)null,
                (Guid? value) => value.HasValue ? new LabTestId(value.Value) : (LabTestId?)null);

        builder.Property(o => o.OrderingDoctorName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.TestName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.TestCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.Priority)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.ResultValue)
            .HasMaxLength(500);

        builder.Property(o => o.ReferenceRange)
            .HasMaxLength(200);

        builder.Property(o => o.Unit)
            .HasMaxLength(50);

        builder.Property(o => o.Remarks)
            .HasMaxLength(1000);

        builder.Property(o => o.PerformedBy)
            .HasMaxLength(200);

        builder.HasOne(o => o.Patient)
            .WithMany()
            .HasForeignKey(o => o.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.LabTest)
            .WithMany()
            .HasForeignKey(o => o.LabTestId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.Priority);
    }
}
