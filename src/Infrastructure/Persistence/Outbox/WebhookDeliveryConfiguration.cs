using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Persistence.Outbox;

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDeliveryRow>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryRow> builder)
    {
        builder.ToTable("webhook_deliveries");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.EventId).HasMaxLength(40).IsRequired();
        builder.Property(r => r.EventType).HasMaxLength(60).IsRequired();
        builder.Property(r => r.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.Status).HasMaxLength(12).IsRequired();
        builder.Property(r => r.LastError).HasMaxLength(1000);

        // El worker barre por (status, next_attempt_at); el log de un endpoint por (endpoint_id).
        builder.HasIndex(r => new { r.Status, r.NextAttemptAt });
        builder.HasIndex(r => r.EndpointId);
    }
}
