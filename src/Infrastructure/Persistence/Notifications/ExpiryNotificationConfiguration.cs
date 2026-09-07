using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Persistence.Notifications;

internal sealed class ExpiryNotificationConfiguration : IEntityTypeConfiguration<ExpiryNotificationRow>
{
    public void Configure(EntityTypeBuilder<ExpiryNotificationRow> builder)
    {
        builder.ToTable("expiry_notifications");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.SubjectType).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Kind).HasMaxLength(40).IsRequired();

        // Un aviso concreto se emite una sola vez.
        builder.HasIndex(r => new { r.SubjectType, r.SubjectId, r.Kind }).IsUnique();
    }
}
