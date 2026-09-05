using ErrorOr;
using FluentValidation;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.CreateApiKey;

/// <summary>
/// Acuña una API key. Recurso de operador: no exige un tenant en la petición.
/// La key <b>lleva su ambiente</b> (Test / Cert / Production) y por eso solo se
/// acuña si el contribuyente ya puede facturar ahí (perfil, certificado activo y
/// algún rango de secuencia). La respuesta lleva el token en claro — única vez que
/// se puede ver.
/// </summary>
public sealed class CreateApiKeyUseCase(
    ILoggerFactory loggerFactory,
    IValidator<CreateApiKeyCommand> validator,
    IEmitterProfileRepository emitterProfiles,
    ICertificateRepository certificates,
    INcfSequenceRepository sequences,
    IApiKeyRepository apiKeys)
    : CommandUseCase<CreateApiKeyCommand, ApiKeyCreatedDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<ApiKeyCreatedDto>> ExecuteCore(
        CreateApiKeyCommand request,
        CancellationToken ct)
    {
        var profile = await emitterProfiles.GetByTenantAsync(request.TenantId, ct);
        if (profile is null)
            return EmitterProfileErrors.NotConfigured;

        var environment = string.IsNullOrWhiteSpace(request.Environment)
            ? profile.DefaultEnvironment
            : DgiiEnvironment.FromName(request.Environment.Trim());
        var role = ApiKeyRole.FromName(request.Role!.Trim());

        var readiness = await CheckReadinessAsync(request.TenantId, environment, ct);
        if (readiness.IsError)
            return readiness.Errors;

        var token = ApiKeyToken.Generate(environment);

        var created = ApiKey.Create(
            request.TenantId,
            ApiKeyToken.Hash(token),
            ApiKeyToken.DisplayPrefix(token),
            request.Label,
            environment,
            role,
            request.ExpiresAt);
        if (created.IsError)
            return created.Errors;

        await apiKeys.AddAsync(created.Value, ct);

        return new ApiKeyCreatedDto(ToDto(created.Value), token);
    }

    private async Task<ErrorOr<Success>> CheckReadinessAsync(
        Guid tenantId, DgiiEnvironment environment, CancellationToken ct)
    {
        var missing = new List<string>();

        if (!await certificates.HasActiveForTenantAsync(tenantId, environment, ct))
            missing.Add("un certificado activo");

        if (!await sequences.HasAnyActiveRangeForTenantAsync(tenantId, environment, ct))
            missing.Add("al menos un rango de secuencia e-NCF");

        return missing.Count == 0
            ? Result.Success
            : ApiKeyErrors.EnvironmentNotReady(environment.Name, missing);
    }

    internal static ApiKeyDto ToDto(ApiKey key) => new(
        key.Id,
        key.TenantId,
        key.Prefix,
        key.Label,
        key.Environment.Name,
        key.Role.Name,
        key.ExpiresAt,
        key.RevokedAt,
        key.LastUsedAt,
        key.CreatedAt);
}
