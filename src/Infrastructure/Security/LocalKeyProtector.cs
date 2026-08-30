using System.Security.Cryptography;
using NovaFE.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace NovaFE.Infrastructure.Security;

/// <summary>
/// <see cref="IKeyProtector"/> con la KEK en configuración. Envuelve la clave de
/// datos con AES-256-GCM. Formato del blob envuelto: <c>nonce(12) || tag(16) || ciphertext</c>.
/// <para>
/// Para pasar a un KMS (AWS / GCP / Azure) se implementa <see cref="IKeyProtector"/>
/// con las llamadas de ese servicio y se cambia el registro en DI. Nada más.
/// </para>
/// </summary>
internal sealed class LocalKeyProtector : IKeyProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _kek;

    public LocalKeyProtector(IOptions<CertificateVaultOptions> options)
    {
        var raw = options.Value.MasterKey;

        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Falta CertificateVault:MasterKey.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(raw);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("CertificateVault:MasterKey no es base64 válido.");
        }

        if (key.Length != 32)
            throw new InvalidOperationException("CertificateVault:MasterKey debe ser exactamente 32 bytes (AES-256).");

        _kek = key;
    }

    public Task<byte[]> WrapAsync(byte[] dataKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataKey);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[dataKey.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_kek, TagSize);
        aes.Encrypt(nonce, dataKey, ciphertext, tag);

        var wrapped = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(wrapped, 0);
        tag.CopyTo(wrapped, NonceSize);
        ciphertext.CopyTo(wrapped, NonceSize + TagSize);

        return Task.FromResult(wrapped);
    }

    public Task<byte[]> UnwrapAsync(byte[] wrappedKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(wrappedKey);

        if (wrappedKey.Length < NonceSize + TagSize)
            throw new CryptographicException("La clave envuelta está truncada.");

        var nonce = wrappedKey.AsSpan(0, NonceSize);
        var tag = wrappedKey.AsSpan(NonceSize, TagSize);
        var ciphertext = wrappedKey.AsSpan(NonceSize + TagSize);
        var dataKey = new byte[ciphertext.Length];

        using var aes = new AesGcm(_kek, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, dataKey);

        return Task.FromResult(dataKey);
    }
}
