using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Persistence.Audit;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogRow>
{
    public void Configure(EntityTypeBuilder<AuditLogRow> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Actor).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ActorRole).HasMaxLength(20);
        builder.Property(r => r.IpAddress).HasMaxLength(64);
        builder.Property(r => r.HttpMethod).HasMaxLength(10).IsRequired();
        builder.Property(r => r.Path).HasMaxLength(256).IsRequired();
        builder.Property(r => r.TraceId).HasMaxLength(64);

        // El listado por tenant pagina por occurred_at descendente.
        builder.HasIndex(r => new { r.TenantId, r.OccurredAt });
    }
}
