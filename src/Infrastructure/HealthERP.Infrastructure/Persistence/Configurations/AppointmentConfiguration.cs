using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HealthERP.Domain.Appointments;
using HealthERP.Domain.Patients;
using HealthERP.Domain.StaffMembers;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments", "scheduling");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AppointmentId(value));

        builder.Property(a => a.PatientId)
            .HasConversion(id => id.Value, value => new PatientId(value))
            .IsRequired();

        builder.Property(a => a.StaffId)
            .HasConversion(id => id.Value, value => new StaffId(value))
            .IsRequired();

        builder.Property(a => a.Reason).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(1000);

        builder.HasOne(a => a.Patient)
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Staff)
            .WithMany()
            .HasForeignKey(a => a.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
