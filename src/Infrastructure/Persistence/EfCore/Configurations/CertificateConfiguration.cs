using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Persistence.EfCore.Configurations;

internal sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Environment)
            .HasConversion(environment => environment.Name, name => DgiiEnvironment.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion(status => status.Name, name => CertificateStatus.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.HolderIdentifier).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Subject).HasMaxLength(1024).IsRequired();
        builder.Property(c => c.Issuer).HasMaxLength(1024).IsRequired();
        builder.Property(c => c.Thumbprint).HasMaxLength(64).IsRequired();
        builder.Property(c => c.VaultReference).HasMaxLength(128).IsRequired();

        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);
        builder.Property(c => c.DeletedBy).HasMaxLength(256);

        // A lo sumo un certificado activo por (tenant, ambiente). El caso de uso
        // ya lo verifica; este índice cierra la ventana de carrera.
        builder.HasIndex(c => new { c.TenantId, c.Environment })
            .IsUnique()
            .HasFilter("status = 'Active' and is_deleted = false");
    }
}
