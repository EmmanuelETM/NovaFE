using System.Security.Cryptography;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Domain.Tenants;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NovaFE.Infrastructure.Security;

/// <summary>
/// Re-envuelve la clave de datos (DEK) de cada fila de <c>certificate_secrets</c>
/// con un <see cref="IKeyProtector"/> distinto. La DEK y el ciphertext del PKCS#12
/// <b>no cambian</b> — solo <c>wrapped_key</c>, la DEK envuelta por la KEK. Se usa
/// una sola vez al cambiar <c>CertificateVault:Provider</c> en un despliegue que ya
/// tiene certificados cargados. Ver <c>docs/certificates.md</c>.
/// <para>
/// Trabaja sobre el tenant actual (lo fija el orquestador antes de resolver este
/// servicio): el filtro global de EF y RLS acotan las filas. Así el trabajo no
/// necesita un rol de base con <c>BYPASSRLS</c>.
/// </para>
/// </summary>
public sealed class CertificateSecretRewrapper(AppDbContext context)
{
    private const int TagSize = 16;

    /// <summary>Todos los ids de tenant (para que el orquestador itere). Sin filtros: incluye tenants borrados.</summary>
    public async Task<IReadOnlyList<Guid>> AllTenantIdsAsync(CancellationToken ct = default)
        => await context.Set<Tenant>()
            .IgnoreQueryFilters()
            .Select(t => t.Id)
            .ToListAsync(ct);

    /// <summary>
    /// Re-envuelve los secretos del tenant actual. Verifica que <paramref name="from"/>
    /// realmente descifra cada fila antes de re-envolver con <paramref name="to"/>;
    /// si no, lanza y no toca nada (todo o nada, una transacción).
    /// </summary>
    public async Task<int> RewrapCurrentTenantAsync(
        IKeyProtector from, IKeyProtector to, ILogger logger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(logger);

        var rows = await context.Set<CertificateSecretRow>().ToListAsync(ct);
        if (rows.Count == 0)
            return 0;

        var rewrapped = 0;
        foreach (var row in rows)
        {
            var dataKey = await from.UnwrapAsync(row.WrappedKey, ct);
            try
            {
                EnsureDecrypts(row, dataKey);
                row.WrappedKey = await to.WrapAsync(dataKey, ct);
                rewrapped++;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }
        }

        await context.SaveChangesAsync(ct);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Re-envueltos {Count} secreto(s) de certificado del tenant en curso.", rewrapped);

        return rewrapped;
    }

    // Confirma que la DEK desenvuelta es la correcta: descifra el payload existente
    // (AES-256-GCM verifica el tag). Si `from` es el protector equivocado, o bien
    // UnwrapAsync ya falló, o esto falla acá — nunca se re-envuelve basura.
    private static void EnsureDecrypts(CertificateSecretRow row, byte[] dataKey)
    {
        var plaintext = new byte[row.Ciphertext.Length];
        try
        {
            using var aes = new AesGcm(dataKey, TagSize);
            aes.Decrypt(row.Nonce, row.Ciphertext, row.Tag, plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
