using HealthERP.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthERP.Infrastructure.Persistence.Configurations;

public class PatientActivationChallengeConfiguration : IEntityTypeConfiguration<PatientActivationChallenge>
{
    public void Configure(EntityTypeBuilder<PatientActivationChallenge> builder)
    {
        builder.ToTable("patient_activation_challenges", "patients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.PatientId)
            .HasConversion(
                patientId => patientId.Value,
                value => new PatientId(value))
            .IsRequired();

        builder.Property(c => c.UserId)
            .IsRequired();

        builder.Property(c => c.CodeHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.ExpiresAtUtc)
            .IsRequired();

        builder.Property(c => c.AttemptCount)
            .IsRequired();

        builder.Property(c => c.IsConsumed)
            .IsRequired();

        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.ExpiresAtUtc);
    }
}
