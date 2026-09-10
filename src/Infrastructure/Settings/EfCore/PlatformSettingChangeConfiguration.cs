using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Settings.EfCore;

internal sealed class PlatformSettingChangeConfiguration : IEntityTypeConfiguration<PlatformSettingChange>
{
    public void Configure(EntityTypeBuilder<PlatformSettingChange> builder)
    {
        builder.ToTable("platform_setting_changes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Key).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Environment).HasMaxLength(20).HasDefaultValue("");
        builder.Property(c => c.PreviousValue).HasMaxLength(4000);
        builder.Property(c => c.NewValue).HasMaxLength(4000);
        builder.Property(c => c.ChangedBy).HasMaxLength(256);

        // El historial de una clave se lee por fecha descendente.
        builder.HasIndex(c => new { c.Key, c.ChangedAt });
    }
}
