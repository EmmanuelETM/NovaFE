namespace NovaFE.Application.Webhooks;

/// <summary>
/// Parámetros de webhooks que necesitan las capas internas. La capa Service los
/// llena desde configuración (<c>WebhooksOptions</c>); los defaults sirven para
/// pruebas y arranque.
/// </summary>
public sealed record WebhookSettings
{
    /// <summary>Tope de endpoints por contribuyente.</summary>
    public int MaxEndpointsPerTenant { get; init; } = 5;

    /// <summary>Exige <c>https</c> en la URL de destino. <c>false</c> en Development (listeners locales).</summary>
    public bool RequireHttps { get; init; } = true;

    /// <summary>Timeout de cada POST de entrega.</summary>
    public TimeSpan DeliveryTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Intentos de entrega antes de marcar la fila <c>dead</c>.</summary>
    public int MaxAttempts { get; init; } = 7;

    /// <summary>
    /// Espera antes de cada reintento. El índice es <c>attempts</c> (el primer
    /// reintento usa <c>[0]</c>); pasado el final se usa el último valor.
    /// </summary>
    public IReadOnlyList<TimeSpan> BackoffLadder { get; init; } =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
    ];

    /// <summary>Fallos de entrega seguidos tras los que un endpoint se deshabilita solo. 0 lo desactiva.</summary>
    public int AutoDisableAfterConsecutiveFailures { get; init; } = 20;

    /// <summary>La espera para el reintento número <paramref name="attempts"/> (0 = el primero).</summary>
    public TimeSpan BackoffFor(int attempts)
    {
        if (BackoffLadder.Count == 0)
            return TimeSpan.FromMinutes(5);

        var index = Math.Clamp(attempts, 0, BackoffLadder.Count - 1);
        return BackoffLadder[index];
    }
}
