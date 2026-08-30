using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Persistence.EfCore.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Rnc)
            .HasConversion(rnc => rnc.Value, value => Rnc.FromStorage(value))
            .HasMaxLength(11)
            .IsRequired();

        // El RNC es único entre contribuyentes vivos. Un contribuyente dado de
        // baja no bloquea volver a registrar el mismo RNC.
        builder.HasIndex(t => t.Rnc)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.Property(t => t.LegalName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(t => t.TradeName)
            .HasMaxLength(150);

        builder.Property(t => t.Plan)
            .HasConversion(plan => plan.Name, name => TenantPlan.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion(status => status.Name, name => TenantStatus.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);
        builder.Property(t => t.DeletedBy).HasMaxLength(256);
    }
}
