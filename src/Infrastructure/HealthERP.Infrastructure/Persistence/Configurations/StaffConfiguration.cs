using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StaffEntity = HealthERP.Domain.StaffMembers.StaffMembers;
using HealthERP.Domain.StaffMembers;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class StaffConfiguration : IEntityTypeConfiguration<StaffEntity>
{
    public void Configure(EntityTypeBuilder<StaffEntity> builder)
    {
        builder.ToTable("staff", "staff");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new StaffId(value));

        builder.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Email).HasMaxLength(256).IsRequired();
        builder.Property(s => s.PhoneNumber).HasMaxLength(20);
        builder.Property(s => s.Specialty).HasMaxLength(100).IsRequired();
        builder.Property(s => s.LicenseNumber).HasMaxLength(50);
        builder.Property(s => s.Department).HasMaxLength(100).IsRequired();

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
