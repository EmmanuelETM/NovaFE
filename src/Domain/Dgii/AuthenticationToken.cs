namespace NovaFE.Domain.Dgii;

/// <summary>
/// El token Bearer que la DGII devuelve al validar la semilla firmada. Vale ~1
/// hora. Se guarda en caché efímera por (tenant, ambiente), nunca en base de
/// datos.
/// </summary>
public sealed record AuthenticationToken
{
    public AuthenticationToken(string value, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (expiresAt <= issuedAt)
            throw new ArgumentException("El token vence antes de haberse expedido.", nameof(expiresAt));

        Value = value;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>El token en sí. Va en el header <c>Authorization: Bearer {value}</c>.</summary>
    public string Value { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Hay que renovar: el token vence dentro de <paramref name="buffer"/>. Se
    /// renueva antes de que expire para que nunca falle una emisión en curso
    /// (RF-01.3).
    /// </summary>
    public bool NeedsRenewal(DateTimeOffset now, TimeSpan buffer) => now >= ExpiresAt - buffer;

    public TimeSpan RemainingLifetime(DateTimeOffset now)
        => ExpiresAt > now ? ExpiresAt - now : TimeSpan.Zero;
}
