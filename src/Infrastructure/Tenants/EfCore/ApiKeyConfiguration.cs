using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Tenants.EfCore;

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();

        // Búsqueda O(1) al autenticar; único (dos tokens no comparten hash).
        builder.HasIndex(k => k.KeyHash)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(k => k.TenantId);

        builder.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.Prefix).HasMaxLength(20).IsRequired();
        builder.Property(k => k.Label).HasMaxLength(ApiKey.MaxLabelLength).IsRequired();

        builder.Property(k => k.Environment)
            .HasConversion(environment => environment.Name, name => DgiiEnvironment.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(k => k.Role)
            .HasConversion(role => role.Name, name => ApiKeyRole.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(k => k.CreatedBy).HasMaxLength(256);
        builder.Property(k => k.UpdatedBy).HasMaxLength(256);
        builder.Property(k => k.DeletedBy).HasMaxLength(256);
    }
}
