using System.Collections.Concurrent;
using NovaFE.Application.Tenants.CreateApiKey;
using NovaFE.Application.Tenants.Interfaces;
using Microsoft.Extensions.Logging;

namespace NovaFE.Infrastructure.Tenants;

/// <summary>
/// Resuelve un token de API key a la identidad del contribuyente: hash → key
/// activa → <see cref="ApiKeyIdentity"/>. Corre en el handler de autenticación,
/// antes de que exista tenant en la petición; la tabla <c>api_keys</c> no lleva
/// RLS, así que la lectura por hash cruza tenants sin problema.
/// </summary>
internal sealed class ApiKeyAuthenticator(
    IApiKeyReadRepository read,
    IApiKeyRepository write,
    TimeProvider timeProvider,
    ILogger<ApiKeyAuthenticator> logger) : IApiKeyAuthenticator
{
    // El "último uso" se escribe como mucho una vez cada 5 min por credencial,
    // para no convertir cada petición autenticada en un UPDATE.
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<Guid, DateTimeOffset> LastTouch = new();

    public async Task<ApiKeyIdentity?> AuthenticateAsync(string presentedToken, CancellationToken ct = default)
    {
        if (!ApiKeyToken.LooksLikeToken(presentedToken))
            return null;

        var lookup = await read.FindByHashAsync(ApiKeyToken.Hash(presentedToken), ct);
        if (lookup is null)
            return null;

        var now = timeProvider.GetUtcNow();
        var usable = lookup.RevokedAt is null
                     && (lookup.ExpiresAt is null || lookup.ExpiresAt > now);
        if (!usable)
            return null;

        await TouchAsync(lookup.Id, now, ct);
        return new ApiKeyIdentity(lookup.Id, lookup.TenantId);
    }

    private async Task TouchAsync(Guid keyId, DateTimeOffset now, CancellationToken ct)
    {
        var last = LastTouch.GetValueOrDefault(keyId);
        if (last != default && now - last < TouchInterval)
            return;

        LastTouch[keyId] = now;

        try
        {
            await write.TouchAsync(keyId, now, ct);
        }
        catch (Exception ex)
        {
            // El "último uso" es telemetría para el operador, no parte del contrato
            // de autenticación: un fallo aquí no debe negar el acceso.
            logger.LogDebug(ex, "No se pudo registrar el último uso de la API key {ApiKeyId}", keyId);
        }
    }
}
