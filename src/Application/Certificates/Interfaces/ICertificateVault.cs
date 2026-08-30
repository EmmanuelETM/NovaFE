using System.Security.Cryptography;

namespace NovaFE.Application.Certificates.Interfaces;

/// <summary>
/// Custodia el PKCS#12 de los certificados. La implementación por defecto cifra
/// con AES-256-GCM y guarda el ciphertext en la base; la clave que lo protege
/// (KEK) vive en configuración hoy y en un KMS mañana, sin cambiar esta interfaz.
/// Otras implementaciones (Supabase Vault, HashiCorp Vault) encajan igual.
/// <para>
/// El tenant se toma de <c>ICurrentTenant</c> dentro de la implementación; el
/// llamador no lo pasa.
/// </para>
/// </summary>
public interface ICertificateVault
{
    /// <summary>Guarda el PKCS#12 y su contraseña. Devuelve una referencia opaca.</summary>
    Task<string> StoreAsync(byte[] pkcs12, string password, CancellationToken ct = default);

    /// <summary>Recupera el material para firmar. El llamador hace <c>Dispose</c>.</summary>
    Task<CertificateSecret> RetrieveAsync(string reference, CancellationToken ct = default);

    Task DeleteAsync(string reference, CancellationToken ct = default);
}

/// <summary>PKCS#12 descifrado. <see cref="Dispose"/> borra los bytes de memoria.</summary>
public sealed class CertificateSecret(byte[] pkcs12, string password) : IDisposable
{
    public byte[] Pkcs12 { get; } = pkcs12;

    public string Password { get; } = password;

    public void Dispose() => CryptographicOperations.ZeroMemory(Pkcs12);
}
