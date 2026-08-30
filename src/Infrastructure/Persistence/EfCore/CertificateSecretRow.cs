using NovaFE.Domain.Common.Entities;

namespace NovaFE.Infrastructure.Persistence.EfCore;

/// <summary>
/// El PKCS#12 cifrado de un certificado. Entidad interna de infraestructura: no
/// existe en el dominio, no la ve la aplicación. Se accede solo desde
/// <c>EnvelopeCertificateVault</c>. Es <see cref="ITenantOwned"/>, así que
/// hereda el aislamiento por tenant (filtro de EF + RLS).
/// <para>
/// El ciphertext incluye el PKCS#12 y su contraseña, cifrados con AES-256-GCM con
/// una clave de datos que a su vez está envuelta por la KEK (<c>IKeyProtector</c>).
/// </para>
/// </summary>
internal sealed class CertificateSecretRow : ITenantOwned
{
    public Guid Reference { get; set; }

    public Guid TenantId { get; private set; }

    public string Algorithm { get; set; } = null!;

    /// <summary>Clave de datos envuelta por la KEK.</summary>
    public byte[] WrappedKey { get; set; } = null!;

    public byte[] Nonce { get; set; } = null!;

    public byte[] Ciphertext { get; set; } = null!;

    public byte[] Tag { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
