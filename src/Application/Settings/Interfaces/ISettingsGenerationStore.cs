namespace NovaFE.Application.Settings.Interfaces;

/// <summary>
/// El contador de generación de <c>settings_generation</c>: cualquier escritura de
/// setting lo incrementa, y cada instancia lo consulta cada pocos segundos para
/// saber si tiene que recargar su snapshot. Ver <c>docs/configuration.md</c>.
/// </summary>
public interface ISettingsGenerationStore
{
    /// <summary>Valor actual del contador.</summary>
    Task<long> CurrentAsync(CancellationToken ct = default);

    /// <summary>Incrementa el contador en 1. Va en la misma transacción que la escritura.</summary>
    Task BumpAsync(CancellationToken ct = default);
}
