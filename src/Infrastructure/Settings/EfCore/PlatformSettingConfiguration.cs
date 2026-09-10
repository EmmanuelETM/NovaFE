using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Settings.EfCore;

internal sealed class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> builder)
    {
        builder.ToTable("platform_settings");

        builder.HasKey(s => new { s.Key, s.Environment });

        builder.Property(s => s.Key).HasMaxLength(200);
        builder.Property(s => s.Environment).HasMaxLength(20).HasDefaultValue("");
        builder.Property(s => s.Value).HasMaxLength(4000).IsRequired();

        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);
    }
}
