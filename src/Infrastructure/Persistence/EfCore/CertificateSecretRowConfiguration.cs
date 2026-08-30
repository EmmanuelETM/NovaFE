using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Persistence.EfCore;

internal sealed class CertificateSecretRowConfiguration : IEntityTypeConfiguration<CertificateSecretRow>
{
    public void Configure(EntityTypeBuilder<CertificateSecretRow> builder)
    {
        builder.ToTable("certificate_secrets");

        builder.HasKey(r => r.Reference);
        builder.Property(r => r.Reference).ValueGeneratedNever();

        builder.Property(r => r.Algorithm).HasMaxLength(32).IsRequired();
        builder.Property(r => r.WrappedKey).IsRequired();
        builder.Property(r => r.Nonce).IsRequired();
        builder.Property(r => r.Ciphertext).IsRequired();
        builder.Property(r => r.Tag).IsRequired();
    }
}
