using NovaFE.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Webhooks.EfCore;

internal sealed class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        builder.ToTable("webhook_endpoints");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.HasIndex(e => e.TenantId);

        builder.Property(e => e.Url).HasMaxLength(WebhookEndpoint.MaxUrlLength).IsRequired();
        builder.Property(e => e.Secret).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(WebhookEndpoint.MaxDescriptionLength);

        // string[] → text[] nativo de Npgsql.
        builder.Property(e => e.Events).HasColumnType("text[]").IsRequired();

        builder.Property(e => e.DisabledReason).HasMaxLength(200);

        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);
    }
}
