using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Settings.GetTenantSettingHistory;

/// <summary>La bitácora de cambios de un setting de tenant. Recurso del contribuyente.</summary>
public sealed class GetTenantSettingHistoryUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    ITenantSettingReadRepository readRepository)
    : QueryUseCase<GetTenantSettingHistoryQuery, IReadOnlyList<TenantSettingChangeDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<TenantSettingChangeDto>>> ExecuteCore(
        GetTenantSettingHistoryQuery request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        if (TenantSettingCatalog.Resolve(request.Key) is null)
            return SettingErrors.UnknownKey(request.Key);

        return ErrorOrFactory.From(
            await readRepository.ListChangesByKeyAsync(tenantId, request.Key, ct));
    }
}
