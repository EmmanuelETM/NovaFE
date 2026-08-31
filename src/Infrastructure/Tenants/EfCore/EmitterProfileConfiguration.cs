using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Tenants.EfCore;

internal sealed class EmitterProfileConfiguration : IEntityTypeConfiguration<EmitterProfile>
{
    public void Configure(EntityTypeBuilder<EmitterProfile> builder)
    {
        builder.ToTable("emitter_profiles");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // 1:1 con el contribuyente. El caso de uso ya hace el upsert; este índice
        // cierra la ventana de carrera.
        builder.HasIndex(p => p.TenantId)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.Property(p => p.Address).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Municipality).HasMaxLength(10);
        builder.Property(p => p.Province).HasMaxLength(10);
        builder.Property(p => p.Email).HasMaxLength(100);
        builder.Property(p => p.EconomicActivity).HasMaxLength(150);

        // text[] nativo de Npgsql (hasta 3 elementos, la invariante la aplica el dominio).
        builder.Property(p => p.Phones)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(p => p.DefaultEnvironment)
            .HasConversion(environment => environment.Name, name => DgiiEnvironment.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);
        builder.Property(p => p.DeletedBy).HasMaxLength(256);
    }
}
