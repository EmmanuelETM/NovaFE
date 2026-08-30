using ErrorOr;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NovaFE.Infrastructure.Dgii;

/// <summary>
/// Orquesta el token de la DGII: caché → (si falta o está por vencer) semilla →
/// firma con el certificado del tenant → validar semilla → guardar en caché.
/// Renovación proactiva y serializada por (tenant, ambiente).
/// </summary>
internal sealed class DgiiTokenProvider(
    ICurrentTenant currentTenant,
    IDgiiTokenCache cache,
    IDgiiAuthClient authClient,
    ICertificateSigner signer,
    DgiiTokenGate gate,
    IOptions<DgiiOptions> options,
    TimeProvider timeProvider,
    ILogger<DgiiTokenProvider> logger) : IDgiiTokenProvider
{
    public async Task<ErrorOr<AuthenticationToken>> GetTokenAsync(
        DgiiEnvironment environment,
        CancellationToken ct = default)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var buffer = options.Value.TokenRenewalBuffer;

        var cached = await cache.GetAsync(tenantId, environment, ct);
        if (IsFresh(cached, buffer))
            return cached!;

        using var _ = await gate.EnterAsync(tenantId, environment, ct);

        // Otra petición pudo renovar mientras esperábamos la compuerta.
        cached = await cache.GetAsync(tenantId, environment, ct);
        if (IsFresh(cached, buffer))
            return cached!;

        var authenticated = await AuthenticateAsync(environment, ct);
        if (authenticated.IsError)
            return authenticated.Errors;

        await cache.SetAsync(tenantId, environment, authenticated.Value, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Token DGII renovado para el tenant {TenantId} en {Environment}; vence {ExpiresAt:o}",
                tenantId, environment.Name, authenticated.Value.ExpiresAt);
        }

        return authenticated.Value;
    }

    private bool IsFresh(AuthenticationToken? token, TimeSpan buffer)
        => token is not null && !token.NeedsRenewal(timeProvider.GetUtcNow(), buffer);

    private async Task<ErrorOr<AuthenticationToken>> AuthenticateAsync(
        DgiiEnvironment environment,
        CancellationToken ct)
    {
        var seed = await authClient.GetSeedAsync(environment, ct);
        if (seed.IsError)
            return seed.Errors;

        var signed = await signer.SignAsync(seed.Value, environment, ct);
        if (signed.IsError)
            return signed.Errors;

        return await authClient.ValidateSeedAsync(environment, signed.Value.Xml, ct);
    }
}
