using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.CreateApiKey;

/// <summary>
/// Acuña una API key. Recurso de operador: no exige un tenant en la petición,
/// pero el contribuyente debe existir. La respuesta lleva el token en claro —
/// única vez que se puede ver.
/// </summary>
public sealed class CreateApiKeyUseCase(
    ILoggerFactory loggerFactory,
    IValidator<CreateApiKeyCommand> validator,
    ITenantReadRepository tenants,
    IApiKeyRepository apiKeys)
    : CommandUseCase<CreateApiKeyCommand, ApiKeyCreatedDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<ApiKeyCreatedDto>> ExecuteCore(
        CreateApiKeyCommand request,
        CancellationToken ct)
    {
        if (await tenants.GetByIdAsync(request.TenantId, ct) is null)
            return TenantErrors.NotFound(request.TenantId);

        var token = ApiKeyToken.Generate();

        var created = ApiKey.Create(
            request.TenantId,
            ApiKeyToken.Hash(token),
            ApiKeyToken.DisplayPrefix(token),
            request.Label,
            request.ExpiresAt);
        if (created.IsError)
            return created.Errors;

        await apiKeys.AddAsync(created.Value, ct);

        return new ApiKeyCreatedDto(ToDto(created.Value), token);
    }

    internal static ApiKeyDto ToDto(ApiKey key) => new(
        key.Id,
        key.TenantId,
        key.Prefix,
        key.Label,
        key.ExpiresAt,
        key.RevokedAt,
        key.LastUsedAt,
        key.CreatedAt);
}
