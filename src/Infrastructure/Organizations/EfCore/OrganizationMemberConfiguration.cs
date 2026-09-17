using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaFE.Domain.Organizations;

namespace NovaFE.Infrastructure.Organizations.EfCore;

internal sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("organization_members");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        // Un usuario tiene a lo sumo una membresía por organización.
        builder.HasIndex(m => new { m.OrganizationId, m.PlatformUserId }).IsUnique();

        builder.HasIndex(m => m.PlatformUserId);

        builder.Property(m => m.Role)
            .HasConversion(role => role.Name, name => OrganizationRole.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.CreatedBy).HasMaxLength(256);
        builder.Property(m => m.UpdatedBy).HasMaxLength(256);
    }
}
