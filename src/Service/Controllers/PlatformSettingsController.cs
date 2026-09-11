using Asp.Versioning;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.GetPlatformSetting;
using NovaFE.Application.Settings.GetPlatformSettingHistory;
using NovaFE.Application.Settings.ListPlatformSettings;
using NovaFE.Application.Settings.ResetPlatformSetting;
using NovaFE.Application.Settings.UpdatePlatformSetting;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Config operativa dinámica de la plataforma (docs/configuration.md). Recurso de
/// <b>operador</b> (header <c>X-Admin-Key</c>). El catálogo de settings vive en
/// código (<c>SettingDefinitions</c>); esta API solo administra los overrides.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.Operator)]
public sealed class PlatformSettingsController(
    ListPlatformSettingsUseCase list,
    GetPlatformSettingUseCase get,
    GetPlatformSettingHistoryUseCase history,
    UpdatePlatformSettingUseCase update,
    ResetPlatformSettingUseCase reset) : ApiController
{
    /// <summary>Todos los settings con su valor efectivo, agrupados en el cliente por <c>group</c>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlatformSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(ct)).Match(Ok, Problem);

    /// <summary>Un setting por su clave.</summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(PlatformSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
        => (await get.Execute(new GetPlatformSettingQuery(key), ct)).Match(Ok, Problem);

    /// <summary>El historial de cambios de un setting, el más reciente primero.</summary>
    [HttpGet("{key}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<PlatformSettingChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(string key, CancellationToken ct)
        => (await history.Execute(new GetPlatformSettingHistoryQuery(key), ct)).Match(Ok, Problem);

    /// <summary>
    /// Crea o reemplaza el override de un setting. Un setting sensible exige
    /// <c>confirm: true</c> en el cuerpo.
    /// </summary>
    [HttpPut("{key}")]
    [ProducesResponseType(typeof(PlatformSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string key,
        [FromBody] SetSettingValueBody body,
        CancellationToken ct)
        => (await update.Execute(
                new UpdatePlatformSettingCommand(key, body.Value, body.Confirm ?? false), ct))
            .Match(Ok, Problem);

    /// <summary>
    /// Quita el override: vuelve a regir el default de código. Un setting sensible
    /// exige <c>?confirm=true</c>.
    /// </summary>
    [HttpDelete("{key}")]
    [ProducesResponseType(typeof(PlatformSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reset(string key, [FromQuery] bool confirm, CancellationToken ct)
        => (await reset.Execute(new ResetPlatformSettingCommand(key, confirm), ct)).Match(Ok, Problem);

    /// <summary>Cuerpo del <c>PUT</c> (la clave va en la ruta).</summary>
    public sealed record SetSettingValueBody(string Value, bool? Confirm);
}
