using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HealthERP.Domain.Billing;
using HealthERP.Domain.Patients;
using HealthERP.Domain.Appointments;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", "billing");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(id => id.Value, value => new InvoiceId(value))
            .ValueGeneratedNever();

        builder.Property(i => i.PatientId)
            .HasConversion(id => id.Value, value => new PatientId(value))
            .IsRequired();

        builder.Property(i => i.AppointmentId)
            .HasConversion(
                (AppointmentId? id) => id.HasValue ? id.Value.Value : (Guid?)null,
                (Guid? value) => value.HasValue ? new AppointmentId(value.Value) : (AppointmentId?)null);

        builder.Property(i => i.InvoiceNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique();

        builder.Property(i => i.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(i => i.PaidAmount)
            .HasPrecision(18, 2);

        builder.HasOne(i => i.Patient)
            .WithMany()
            .HasForeignKey(i => i.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Appointment)
            .WithMany()
            .HasForeignKey(i => i.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(i => i.LineItems)
            .WithOne()
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Payments)
            .WithOne()
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("invoice_line_items", "billing");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.InvoiceId)
            .HasConversion(id => id.Value, value => new InvoiceId(value))
            .IsRequired();

        builder.Property(l => l.Description)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(l => l.UnitPrice)
            .HasPrecision(18, 2);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", "billing");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.InvoiceId)
            .HasConversion(id => id.Value, value => new InvoiceId(value))
            .IsRequired();

        builder.Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Property(p => p.ReferenceNumber)
            .HasMaxLength(100);

        builder.Property(p => p.Notes)
            .HasMaxLength(500);
    }
}
