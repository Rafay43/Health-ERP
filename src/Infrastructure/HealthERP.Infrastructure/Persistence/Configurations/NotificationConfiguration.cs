using HealthERP.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", "notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .HasConversion(
                id => id.Value,
                value => new NotificationId(value));

        builder.Property(n => n.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.Message)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(n => n.TargetRole)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.LinkUrl)
            .HasMaxLength(500);

        builder.HasIndex(n => n.TargetUserId);
        builder.HasIndex(n => n.TargetRole);
        builder.HasIndex(n => n.IsRead);
        builder.HasIndex(n => n.CreatedAtUtc);
    }
}
