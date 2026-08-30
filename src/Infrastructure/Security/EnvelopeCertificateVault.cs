using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Security;

/// <summary>
/// <see cref="ICertificateVault"/> por defecto: cifra cada PKCS#12 con AES-256-GCM
/// usando una clave de datos aleatoria por secreto, que a su vez se envuelve con
/// la KEK (<see cref="IKeyProtector"/>). El ciphertext se guarda en la base; la
/// KEK nunca. Un volcado de la base solo expone ciphertext.
/// <para>
/// Es una de varias implementaciones posibles de <see cref="ICertificateVault"/>
/// (Supabase Vault, HashiCorp Vault…); el resto del sistema no lo nota.
/// </para>
/// </summary>
internal sealed class EnvelopeCertificateVault(
    AppDbContext context,
    IKeyProtector keyProtector,
    TimeProvider timeProvider) : ICertificateVault
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string Algorithm = "AES-256-GCM";

    public async Task<string> StoreAsync(byte[] pkcs12, string password, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pkcs12);
        ArgumentNullException.ThrowIfNull(password);

        var payload = Pack(pkcs12, password);
        var dataKey = RandomNumberGenerator.GetBytes(32);

        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var ciphertext = new byte[payload.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(dataKey, TagSize))
                aes.Encrypt(nonce, payload, ciphertext, tag);

            var wrappedKey = await keyProtector.WrapAsync(dataKey, ct);
            var reference = Guid.CreateVersion7();

            context.Set<CertificateSecretRow>().Add(new CertificateSecretRow
            {
                Reference = reference,
                Algorithm = Algorithm,
                WrappedKey = wrappedKey,
                Nonce = nonce,
                Ciphertext = ciphertext,
                Tag = tag,
                CreatedAt = timeProvider.GetUtcNow(),
            });

            await context.SaveChangesAsync(ct);

            return reference.ToString();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public async Task<CertificateSecret> RetrieveAsync(string reference, CancellationToken ct = default)
    {
        var row = await FindAsync(reference, ct)
                  ?? throw new InvalidOperationException($"No hay un secreto de certificado con referencia '{reference}'.");

        var dataKey = await keyProtector.UnwrapAsync(row.WrappedKey, ct);

        try
        {
            var payload = new byte[row.Ciphertext.Length];

            using (var aes = new AesGcm(dataKey, TagSize))
                aes.Decrypt(row.Nonce, row.Ciphertext, row.Tag, payload);

            var (pkcs12, password) = Unpack(payload);
            CryptographicOperations.ZeroMemory(payload);

            return new CertificateSecret(pkcs12, password);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public async Task DeleteAsync(string reference, CancellationToken ct = default)
    {
        var row = await FindAsync(reference, ct);
        if (row is null)
            return;

        context.Set<CertificateSecretRow>().Remove(row);
        await context.SaveChangesAsync(ct);
    }

    private Task<CertificateSecretRow?> FindAsync(string reference, CancellationToken ct)
        => Guid.TryParse(reference, out var id)
            ? context.Set<CertificateSecretRow>().FirstOrDefaultAsync(r => r.Reference == id, ct)
            : Task.FromResult<CertificateSecretRow?>(null);

    // payload = [4-byte LE length of pkcs12] || pkcs12 || password(utf8)
    private static byte[] Pack(byte[] pkcs12, string password)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var payload = new byte[4 + pkcs12.Length + passwordBytes.Length];

        BinaryPrimitives.WriteInt32LittleEndian(payload, pkcs12.Length);
        pkcs12.CopyTo(payload, 4);
        passwordBytes.CopyTo(payload, 4 + pkcs12.Length);

        return payload;
    }

    private static (byte[] Pkcs12, string Password) Unpack(byte[] payload)
    {
        var pkcs12Length = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var pkcs12 = payload[4..(4 + pkcs12Length)];
        var password = Encoding.UTF8.GetString(payload, 4 + pkcs12Length, payload.Length - 4 - pkcs12Length);

        return (pkcs12, password);
    }
}
