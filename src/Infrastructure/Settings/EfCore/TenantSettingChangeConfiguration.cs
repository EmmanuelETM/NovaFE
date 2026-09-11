using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Settings.EfCore;

internal sealed class TenantSettingChangeConfiguration : IEntityTypeConfiguration<TenantSettingChange>
{
    public void Configure(EntityTypeBuilder<TenantSettingChange> builder)
    {
        builder.ToTable("tenant_setting_changes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Key).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Environment).HasMaxLength(20).HasDefaultValue("");
        builder.Property(c => c.PreviousValue).HasMaxLength(4000);
        builder.Property(c => c.NewValue).HasMaxLength(4000);
        builder.Property(c => c.ChangedBy).HasMaxLength(256);

        // El historial de una clave de un tenant se lee por fecha descendente.
        builder.HasIndex(c => new { c.TenantId, c.Key, c.ChangedAt });
    }
}
