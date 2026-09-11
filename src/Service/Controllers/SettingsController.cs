using Asp.Versioning;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.GetTenantSettingHistory;
using NovaFE.Application.Settings.ListTenantSettings;
using NovaFE.Application.Settings.ResetTenantSetting;
using NovaFE.Application.Settings.UpdateTenantSetting;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Configuración que el propio contribuyente ajusta (self-serve). Recurso
/// <b>por contribuyente</b>, política <c>TenantConfig</c> (rol <c>admin_tenant</c>).
/// El catálogo de settings vive en código (<c>SettingDefinitions</c>, scope
/// <c>Tenant</c>); esta API solo administra los overrides del tenant. Ver
/// <c>docs/configuration.md</c>.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.TenantConfig)]
public sealed class SettingsController(
    ListTenantSettingsUseCase list,
    GetTenantSettingHistoryUseCase history,
    UpdateTenantSettingUseCase update,
    ResetTenantSettingUseCase reset) : ApiController
{
    /// <summary>Los settings que el contribuyente puede ajustar, con su valor efectivo.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(ct)).Match(Ok, Problem);

    /// <summary>El historial de cambios de un setting, el más reciente primero.</summary>
    [HttpGet("{key}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantSettingChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(string key, CancellationToken ct)
        => (await history.Execute(new GetTenantSettingHistoryQuery(key), ct)).Match(Ok, Problem);

    /// <summary>Crea o reemplaza el override de un setting para el contribuyente.</summary>
    [HttpPut("{key}")]
    [ProducesResponseType(typeof(TenantSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string key,
        [FromBody] SetTenantSettingValueBody body,
        CancellationToken ct)
        => (await update.Execute(new UpdateTenantSettingCommand(key, body.Value), ct)).Match(Ok, Problem);

    /// <summary>Quita el override: vuelve a regir la capa de plataforma o el default.</summary>
    [HttpDelete("{key}")]
    [ProducesResponseType(typeof(TenantSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reset(string key, CancellationToken ct)
        => (await reset.Execute(new ResetTenantSettingCommand(key), ct)).Match(Ok, Problem);

    /// <summary>Cuerpo del <c>PUT</c> (la clave va en la ruta).</summary>
    public sealed record SetTenantSettingValueBody(string Value);
}
