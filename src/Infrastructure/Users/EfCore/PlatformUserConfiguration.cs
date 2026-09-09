using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaFE.Domain.Users;

namespace NovaFE.Infrastructure.Users.EfCore;

internal sealed class PlatformUserConfiguration : IEntityTypeConfiguration<PlatformUser>
{
    public void Configure(EntityTypeBuilder<PlatformUser> builder)
    {
        builder.ToTable("platform_users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        // El correo es la clave de alta: un usuario vivo por correo.
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("is_deleted = false");

        // La cuenta de Better Auth se enlaza una vez; único cuando no es null.
        builder.HasIndex(u => u.AuthUserId)
            .IsUnique()
            .HasFilter("auth_user_id IS NOT NULL AND is_deleted = false");

        builder.HasIndex(u => u.TenantId);

        builder.Property(u => u.Email).HasMaxLength(PlatformUser.MaxEmailLength).IsRequired();
        builder.Property(u => u.AuthUserId).HasMaxLength(PlatformUser.MaxAuthUserIdLength);

        builder.Property(u => u.Role)
            .HasConversion(role => role.Name, name => PlatformRole.FromName(name))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.CreatedBy).HasMaxLength(256);
        builder.Property(u => u.UpdatedBy).HasMaxLength(256);
        builder.Property(u => u.DeletedBy).HasMaxLength(256);
    }
}
