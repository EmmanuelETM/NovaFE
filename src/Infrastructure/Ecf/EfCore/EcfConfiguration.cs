using System.Text.Json;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Sequences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Ecf.EfCore;

internal sealed class EcfConfiguration : IEntityTypeConfiguration<IssuedEcf>
{
    public void Configure(EntityTypeBuilder<IssuedEcf> builder)
    {
        builder.ToTable("issued_ecf");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Type)
            .HasColumnName("ecf_type")
            .HasConversion(type => (short)type.Id, id => EcfType.FromValue(id))
            .IsRequired();

        builder.Property(e => e.Environment)
            .HasConversion(environment => environment.Name, name => DgiiEnvironment.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Encf)
            .HasConversion(encf => encf.Value, value => Encf.FromStorage(value))
            .HasMaxLength(13)
            .IsRequired();

        // Se guarda por el nombre público (el mismo token que expone la API).
        builder.Property(e => e.Status)
            .HasConversion(
                status => status.PublicName,
                name => EcfStatus.GetAll().First(status => status.PublicName == name))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.InternalInvoiceNumber).HasMaxLength(20);
        builder.Property(e => e.BuyerRnc).HasMaxLength(11);
        builder.Property(e => e.BuyerName).HasMaxLength(150);

        builder.Property(e => e.MontoTotal).HasPrecision(18, 2);

        // Totales curados como un único jsonb (el resultado completo del motor está en el XML).
        builder.Property(e => e.Totals)
            .HasColumnType("jsonb")
            .HasConversion(
                snapshot => JsonSerializer.Serialize(snapshot, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<EcfTotalsSnapshot>(json, (JsonSerializerOptions?)null)!,
                new ValueComparer<EcfTotalsSnapshot>(
                    (a, b) => a == b,
                    v => v == null ? 0 : v.GetHashCode(),
                    v => v!))
            .IsRequired();

        builder.Property(e => e.SignatureValue).IsRequired();
        builder.Property(e => e.SecurityCode).HasMaxLength(6).IsRequired();
        builder.Property(e => e.DocumentHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.QrUrl).IsRequired();

        builder.Property(e => e.EcfXml).HasColumnType("text").IsRequired();
        builder.Property(e => e.RfceXml).HasColumnType("text");

        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        // Dedup de negocio: un comprobante por (tenant, NumeroFacturaInterna).
        builder.HasIndex(e => new { e.TenantId, e.InternalInvoiceNumber })
            .IsUnique()
            .HasFilter("internal_invoice_number is not null and is_deleted = false");

        // Listado (orden por fecha) y búsqueda por e-NCF.
        builder.HasIndex(e => new { e.TenantId, e.CreatedAt });
        builder.HasIndex(e => new { e.TenantId, e.Encf });
    }
}
