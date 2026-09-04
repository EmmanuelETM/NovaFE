using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.RevokeApiKey;

/// <summary>
/// Revoca una API key. Recurso de operador: el contribuyente va en la ruta, no
/// hace falta un tenant en la petición. Idempotencia estricta la aplica el
/// dominio (revocar dos veces → conflicto).
/// </summary>
public sealed class RevokeApiKeyUseCase(
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider,
    IApiKeyRepository apiKeys)
    : CommandUseCase<RevokeApiKeyCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        RevokeApiKeyCommand request,
        CancellationToken ct)
    {
        var key = await apiKeys.GetAsync(request.KeyId, request.TenantId, ct);
        if (key is null)
            return ApiKeyErrors.NotFound(request.KeyId);

        var revoked = key.Revoke(timeProvider.GetUtcNow());
        if (revoked.IsError)
            return revoked.Errors;

        await apiKeys.UpdateAsync(key, ct);
        return Result.Success;
    }
}
