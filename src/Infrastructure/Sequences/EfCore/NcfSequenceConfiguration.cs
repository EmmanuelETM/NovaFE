using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NovaFE.Infrastructure.Sequences.EfCore;

internal sealed class NcfSequenceConfiguration : IEntityTypeConfiguration<NcfSequence>
{
    public void Configure(EntityTypeBuilder<NcfSequence> builder)
    {
        builder.ToTable("ncf_sequences");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Environment)
            .HasConversion(environment => environment.Name, name => DgiiEnvironment.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        // El tipo se guarda como su código de dos dígitos (31, 32, …): coincide con
        // lo que va embebido en el e-NCF y con la columna SMALLINT del diseño DGII.
        builder.Property(s => s.Type)
            .HasColumnName("ecf_type")
            .HasConversion(type => (short)type.Id, id => EcfType.FromValue(id))
            .IsRequired();

        builder.Property(s => s.Series)
            .HasConversion(series => series.ToString(), value => value[0])
            .HasColumnType("char(1)")
            .IsRequired();

        builder.Property(s => s.RangeFrom).IsRequired();
        builder.Property(s => s.RangeTo).IsRequired();
        builder.Property(s => s.Next).IsRequired();
        builder.Property(s => s.ExpiresOn).HasColumnType("date");
        builder.Property(s => s.Active).IsRequired();

        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);
        builder.Property(s => s.DeletedBy).HasMaxLength(256);

        // A lo sumo un rango activo por (tenant, ambiente, tipo, serie). El caso de
        // uso ya lo verifica; este índice cierra la ventana de carrera.
        builder.HasIndex(s => new { s.TenantId, s.Environment, s.Type, s.Series })
            .IsUnique()
            .HasFilter("active and is_deleted = false");
    }
}
