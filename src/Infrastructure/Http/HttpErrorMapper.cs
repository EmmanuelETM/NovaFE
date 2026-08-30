using NovaFE.Domain.Common;
using ErrorOr;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace NovaFE.Infrastructure.Http;

/// <summary>
/// Traduce los fallos de un cliente HTTP resiliente a los errores de dominio
/// de <see cref="Errors.Http"/>, para que un caso de uso pueda devolver un
/// <c>ErrorOr</c> en lugar de dejar escapar excepciones de infraestructura.
/// </summary>
/// <example>
/// <code>
/// try
/// {
///     var respuesta = await _http.GetFromJsonAsync&lt;ComprobanteDto&gt;(url, ct);
///     return respuesta!;
/// }
/// catch (Exception ex)
/// {
///     _logger.LogWarning(ex, "Falló la llamada a ECFGateway");
///     return HttpErrorMapper.Map(ex);
/// }
/// </code>
/// </example>
public static class HttpErrorMapper
{
    public static Error Map(Exception exception) => exception switch
    {
        // El circuito abierto se evalúa primero: mientras está abierto, ni
        // siquiera se intenta la llamada.
        BrokenCircuitException => Errors.Http.CircuitOpen,

        TimeoutRejectedException => Errors.Http.Timeout,

        TaskCanceledException => Errors.Http.Timeout,

        HttpRequestException => Errors.Http.Unreachable,

        _ => Errors.Http.RequestFailed
    };
}
