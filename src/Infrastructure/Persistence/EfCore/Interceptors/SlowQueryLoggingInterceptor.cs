using System.Data.Common;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace NovaFE.Infrastructure.Persistence.EfCore.Interceptors;

/// <summary>
/// Loguea en Warning los comandos SQL que superan
/// <see cref="SettingDefinitions.SlowQueryThresholdMs"/>. Los logs normales de
/// "Executed DbCommand" de EF Core están silenciados en <c>appsettings.json</c>
/// (categoría <c>Microsoft</c> en Warning); esto es la señal que sobrevive a ese
/// silencio para lo que realmente importa. Ver <c>docs/observability.md</c>.
/// </summary>
public sealed class SlowQueryLoggingInterceptor(
    ISettingsReader settingsReader,
    ILogger<SlowQueryLoggingInterceptor> logger) : DbCommandInterceptor
{
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void LogIfSlow(DbCommand command, CommandExecutedEventData eventData)
    {
        var threshold = settingsReader.GetValue(SettingDefinitions.SlowQueryThresholdMs);

        if (eventData.Duration.TotalMilliseconds >= threshold)
            logger.LogWarning(
                "Consulta lenta ({DurationMs} ms, umbral {ThresholdMs} ms): {CommandText}",
                eventData.Duration.TotalMilliseconds, threshold, command.CommandText);
    }
}
