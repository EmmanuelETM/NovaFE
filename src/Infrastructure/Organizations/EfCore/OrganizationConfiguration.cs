using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaFE.Domain.Organizations;

namespace NovaFE.Infrastructure.Organizations.EfCore;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Name)
            .HasMaxLength(Organization.MaxNameLength)
            .IsRequired();

        builder.Property(o => o.Slug)
            .HasMaxLength(Organization.MaxSlugLength)
            .IsRequired();

        // El slug es único entre organizaciones vivas, igual que el RNC de Tenant.
        builder.HasIndex(o => o.Slug)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.Property(o => o.Plan)
            .HasConversion(plan => plan.Name, name => OrganizationPlan.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion(status => status.Name, name => OrganizationStatus.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.CreatedBy).HasMaxLength(256);
        builder.Property(o => o.UpdatedBy).HasMaxLength(256);
        builder.Property(o => o.DeletedBy).HasMaxLength(256);
    }
}
