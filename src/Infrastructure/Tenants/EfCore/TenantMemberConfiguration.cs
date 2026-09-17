using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaFE.Domain.Tenants;
using NovaFE.Domain.Users;

namespace NovaFE.Infrastructure.Tenants.EfCore;

internal sealed class TenantMemberConfiguration : IEntityTypeConfiguration<TenantMember>
{
    public void Configure(EntityTypeBuilder<TenantMember> builder)
    {
        builder.ToTable("tenant_members");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        // Un usuario tiene a lo sumo una membresía por tenant.
        builder.HasIndex(m => new { m.TenantId, m.PlatformUserId }).IsUnique();

        builder.HasIndex(m => m.PlatformUserId);

        builder.Property(m => m.Role)
            .HasConversion(role => role.Name, name => PlatformRole.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.CreatedBy).HasMaxLength(256);
        builder.Property(m => m.UpdatedBy).HasMaxLength(256);
    }
}
