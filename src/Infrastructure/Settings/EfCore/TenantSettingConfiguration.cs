using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Settings.EfCore;

internal sealed class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
        builder.ToTable("tenant_settings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Key).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Environment).HasMaxLength(20).HasDefaultValue("");
        builder.Property(s => s.Value).HasMaxLength(4000).IsRequired();

        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);

        // Un override por (tenant, clave, ambiente); y el índice sobre tenant_id que
        // pide docs/multi-tenancy.md para una entidad ITenantOwned.
        builder.HasIndex(s => new { s.TenantId, s.Key, s.Environment }).IsUnique();
    }
}
