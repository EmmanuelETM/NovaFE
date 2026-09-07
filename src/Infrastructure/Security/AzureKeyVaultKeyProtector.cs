using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using NovaFE.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace NovaFE.Infrastructure.Security;

/// <summary>
/// <see cref="IKeyProtector"/> respaldado por Azure Key Vault: envuelve y
/// desenvuelve la clave de datos (DEK) con una clave RSA que vive en el vault
/// (algoritmo <c>RSA-OAEP-256</c>). La operación criptográfica ocurre en el vault;
/// la KEK nunca entra en el proceso. Es la Fase 1.5 del roadmap de
/// <c>docs/certificates.md</c>.
/// <para>
/// La credencial la resuelve <see cref="DefaultAzureCredential"/>: en Azure
/// Container Apps es la managed identity asignada a la app (rol
/// <c>Key Vault Crypto User</c> sobre el vault); en local, <c>az login</c> o las
/// variables de entorno <c>AZURE_*</c>. Sin estado → singleton.
/// </para>
/// </summary>
internal sealed class AzureKeyVaultKeyProtector : IKeyProtector
{
    private readonly CryptographyClient _client;

    public AzureKeyVaultKeyProtector(IOptions<CertificateVaultOptions> options)
        : this(options, new DefaultAzureCredential())
    {
    }

    // Sobrecarga para pruebas: permite inyectar una credencial distinta.
    internal AzureKeyVaultKeyProtector(IOptions<CertificateVaultOptions> options, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);

        var uri = options.Value.KeyVaultKeyUri;
        if (string.IsNullOrWhiteSpace(uri))
            throw new InvalidOperationException(
                "Falta CertificateVault:KeyVaultKeyUri (Provider = azure-key-vault).");

        _client = new CryptographyClient(new Uri(uri), credential);
    }

    public async Task<byte[]> WrapAsync(byte[] dataKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataKey);

        var result = await _client.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, dataKey, ct);
        return result.EncryptedKey;
    }

    public async Task<byte[]> UnwrapAsync(byte[] wrappedKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(wrappedKey);

        var result = await _client.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, wrappedKey, ct);
        return result.Key;
    }
}
