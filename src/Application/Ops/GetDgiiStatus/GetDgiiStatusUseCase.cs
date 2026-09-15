using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Application.Ops.Contracts;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Ops.GetDgiiStatus;

/// <summary>
/// Diagnóstico del estatus de servicio de la DGII (Módulo 10). Llama a los tres
/// endpoints de <see cref="IDgiiStatusClient"/> — un fallo en uno no tumba a los
/// otros, cada sección trae su propio dato o su propio error.
/// <see cref="DgiiStatusDiagnosticDto.Verified"/> siempre <c>false</c>: es
/// lectura tolerante, no una decisión automática (ver <c>docs/dgii-queries.md</c>).
/// </summary>
public sealed class GetDgiiStatusUseCase(ILoggerFactory loggerFactory, IDgiiStatusClient client)
    : ParameterlessQueryUseCase<DgiiStatusDiagnosticDto>(loggerFactory)
{
    protected override async Task<ErrorOr<DgiiStatusDiagnosticDto>> ExecuteCore(NoRequest request, CancellationToken ct)
    {
        var services = await client.GetServiceStatusAsync(ct);
        var windows = await client.GetMaintenanceWindowsAsync(ct);

        var environments = new List<DgiiEnvironmentStatusEntryDto>();
        foreach (var environment in DgiiEnvironment.GetAll())
        {
            var status = await client.VerifyEnvironmentStatusAsync(environment, ct);
            environments.Add(new DgiiEnvironmentStatusEntryDto(
                environment.Name,
                status.IsError ? null : status.Value,
                status.IsError ? status.FirstError.Description : null));
        }

        return new DgiiStatusDiagnosticDto(
            Verified: false,
            Services: new(services.IsError ? null : services.Value, services.IsError ? services.FirstError.Description : null),
            MaintenanceWindows: new(windows.IsError ? null : windows.Value, windows.IsError ? windows.FirstError.Description : null),
            Environments: environments);
    }
}
