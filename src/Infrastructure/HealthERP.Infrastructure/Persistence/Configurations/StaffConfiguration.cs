using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StaffEntity = HealthERP.Domain.StaffMembers.StaffMembers;
using HealthERP.Domain.StaffMembers;
using HealthERP.Infrastructure.Identity;

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

        builder.Property(s => s.UserId).IsRequired(false);
        builder.Property(s => s.StaffKind).HasDefaultValue(StaffKind.Unspecified);
        // Deleted/inactive Staff still reserve the account until explicitly unlinked.
        builder.HasIndex(s => s.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
        builder.HasOne<ApplicationUser>().WithOne()
            .HasForeignKey<StaffEntity>(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
