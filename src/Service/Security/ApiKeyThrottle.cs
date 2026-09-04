using System.Collections.Concurrent;

namespace NovaFE.Service.Security;

/// <summary>
/// Protección contra fuerza bruta en la autenticación (RF-14.6): cuenta los
/// intentos fallidos por origen y bloquea temporalmente tras superar el umbral.
/// </summary>
public interface IApiKeyThrottle
{
    /// <summary>¿El origen está bloqueado ahora mismo?</summary>
    bool IsBlocked(string client);

    /// <summary>Registra un intento fallido; puede dejar al origen bloqueado.</summary>
    void RegisterFailure(string client);

    /// <summary>Un intento válido limpia el historial del origen.</summary>
    void RegisterSuccess(string client);
}

/// <summary>
/// Implementación en memoria (una instancia). Cinco fallos en 5 minutos → bloqueo
/// de 15. Suficiente a la escala de arranque; si hubiera varias instancias, esto
/// pasaría a la caché distribuida.
/// </summary>
internal sealed class InMemoryApiKeyThrottle(TimeProvider timeProvider) : IApiKeyThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BlockFor = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Attempts> _byClient = new();

    private sealed record Attempts(int Failures, DateTimeOffset WindowStart, DateTimeOffset? BlockedUntil);

    public bool IsBlocked(string client) =>
        _byClient.TryGetValue(client, out var a)
        && a.BlockedUntil is { } until
        && until > timeProvider.GetUtcNow();

    public void RegisterFailure(string client)
    {
        var now = timeProvider.GetUtcNow();

        _byClient.AddOrUpdate(
            client,
            _ => new Attempts(1, now, null),
            (_, current) =>
            {
                if (now - current.WindowStart > Window)
                    return new Attempts(1, now, null);

                var failures = current.Failures + 1;
                return failures >= MaxFailures
                    ? new Attempts(failures, current.WindowStart, now + BlockFor)
                    : current with { Failures = failures };
            });
    }

    public void RegisterSuccess(string client) => _byClient.TryRemove(client, out _);
}
