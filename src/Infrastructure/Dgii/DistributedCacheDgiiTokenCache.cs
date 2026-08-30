using System.Text.Json;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;
using Microsoft.Extensions.Caching.Distributed;

namespace NovaFE.Infrastructure.Dgii;

/// <summary>
/// Caché del token de la DGII sobre <see cref="IDistributedCache"/> (hoy en
/// memoria; con Redis igual). La entrada expira sola cuando el token vence.
/// </summary>
internal sealed class DistributedCacheDgiiTokenCache(
    IDistributedCache cache,
    TimeProvider timeProvider) : IDgiiTokenCache
{
    public async Task<AuthenticationToken?> GetAsync(
        Guid tenantId,
        DgiiEnvironment environment,
        CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(Key(tenantId, environment), ct);
        if (bytes is null || bytes.Length == 0)
            return null;

        Entry? entry;
        try
        {
            entry = JsonSerializer.Deserialize<Entry>(bytes);
        }
        catch (JsonException)
        {
            return null;
        }

        if (entry is null || string.IsNullOrWhiteSpace(entry.Value) || entry.ExpiresAt <= entry.IssuedAt)
            return null;

        var token = new AuthenticationToken(entry.Value, entry.IssuedAt, entry.ExpiresAt);
        return token.IsExpired(timeProvider.GetUtcNow()) ? null : token;
    }

    public Task SetAsync(
        Guid tenantId,
        DgiiEnvironment environment,
        AuthenticationToken token,
        CancellationToken ct = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new Entry(token.Value, token.IssuedAt, token.ExpiresAt));

        return cache.SetAsync(
            Key(tenantId, environment),
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpiration = token.ExpiresAt },
            ct);
    }

    public Task RemoveAsync(Guid tenantId, DgiiEnvironment environment, CancellationToken ct = default)
        => cache.RemoveAsync(Key(tenantId, environment), ct);

    private static string Key(Guid tenantId, DgiiEnvironment environment)
        => $"dgii:token:{environment.Name}:{tenantId}";

    private sealed record Entry(string Value, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
}
