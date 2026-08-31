using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Persistence.Outbox;

internal sealed class EcfSubmissionOutboxConfiguration : IEntityTypeConfiguration<EcfSubmissionOutboxRow>
{
    public void Configure(EntityTypeBuilder<EcfSubmissionOutboxRow> builder)
    {
        builder.ToTable("ecf_submission_outbox");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Environment).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Kind).HasMaxLength(10).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(12).IsRequired();
        builder.Property(r => r.TrackId).HasMaxLength(50);

        // El worker barre por (status, next_attempt_at); una fila pendiente por e-CF.
        builder.HasIndex(r => new { r.Status, r.NextAttemptAt });
        builder.HasIndex(r => r.EcfId);
    }
}
