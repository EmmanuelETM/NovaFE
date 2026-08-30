using System.Diagnostics;
using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Common;

/// <summary>
/// Comportamiento transversal compartido por todos los casos de uso:
/// validación con FluentValidation, logging de entrada/salida y medición de duración.
/// <para>
/// No heredes de esta clase directamente: usa <see cref="CommandUseCase{TRequest,TResponse}"/>
/// para operaciones de escritura y <see cref="QueryUseCase{TRequest,TResponse}"/> para lectura.
/// </para>
/// </summary>
public abstract class UseCaseBase<TRequest, TResponse> : IUseCase<TRequest, TResponse>
{
    private readonly IValidator<TRequest>? _validator;

    // Se resuelven una sola vez por instancia: los argumentos de log deben ser baratos.
    private readonly string _kind;
    private readonly string _useCaseName;

    /// <param name="kind">Etiqueta que distingue commands de queries en el log.</param>
    /// <param name="loggerFactory">Fábrica de loggers inyectada por el contenedor.</param>
    /// <param name="validator">Validador opcional; si existe, se ejecuta antes de <see cref="ExecuteCore"/>.</param>
    protected UseCaseBase(string kind, ILoggerFactory loggerFactory, IValidator<TRequest>? validator = null)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _kind = kind;
        _useCaseName = GetType().Name;
        _validator = validator;

        Logger = loggerFactory.CreateLogger(GetType());
    }

    /// <summary>Logger ya etiquetado con el tipo concreto del caso de uso.</summary>
    protected ILogger Logger { get; }

    public async Task<ErrorOr<TResponse>> Execute(TRequest request, CancellationToken ct = default)
    {
        // Los logs de Information se guardan con IsEnabled: si el nivel está apagado
        // (típico en producción) se evita la asignación del array de argumentos.
        if (Logger.IsEnabled(LogLevel.Information))
            Logger.LogInformation("[START] {Kind} {UseCase}", _kind, _useCaseName);

        var timer = Stopwatch.StartNew();

        try
        {
            if (_validator is not null)
            {
                var validation = await _validator.ValidateAsync(request, ct);

                if (!validation.IsValid)
                {
                    var errors = validation.Errors
                        .Select(e => Error.Validation(
                            code: e.PropertyName,
                            description: e.ErrorMessage))
                        .ToList();

                    Logger.LogWarning(
                        "[INVALID] {Kind} {UseCase} — {ErrorCount} error(es) de validación",
                        _kind, _useCaseName, errors.Count);

                    return errors;
                }
            }

            var result = await ExecuteCore(request, ct);
            timer.Stop();

            if (result.IsError)
            {
                Logger.LogWarning(
                    "[FAILED] {Kind} {UseCase} ({Ms}ms) — {Error}",
                    _kind, _useCaseName, timer.ElapsedMilliseconds, result.FirstError.Description);
            }
            else if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation(
                    "[END] {Kind} {UseCase} ({Ms}ms)",
                    _kind, _useCaseName, timer.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            timer.Stop();

            Logger.LogError(
                ex, "[EXCEPTION] {Kind} {UseCase} falló tras {Ms}ms",
                _kind, _useCaseName, timer.ElapsedMilliseconds);

            // Se relanza a propósito: GlobalExceptionHandler decide la respuesta HTTP.
            throw;
        }
    }

    /// <summary>Aquí va la lógica de negocio. La validación y el logging ya están resueltos.</summary>
    protected abstract Task<ErrorOr<TResponse>> ExecuteCore(TRequest request, CancellationToken ct);
}
