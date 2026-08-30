namespace NovaFE.Domain.Common;

/// <summary>
/// Atajos sobre <see cref="TimeProvider"/> para la hora dominicana. Se inyecta
/// <see cref="TimeProvider"/> como siempre (las pruebas controlan el reloj); estos
/// métodos convierten a <see cref="DominicanTimeZone"/> en el borde.
/// </summary>
public static class TimeProviderExtensions
{
    /// <summary>Ahora, en hora local dominicana (offset -04:00).</summary>
    public static DateTimeOffset GetDominicanNow(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return DominicanTimeZone.ToLocal(timeProvider.GetUtcNow());
    }

    /// <summary>La fecha de hoy en el calendario dominicano.</summary>
    public static DateOnly GetDominicanToday(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return DominicanTimeZone.LocalDate(timeProvider.GetUtcNow());
    }
}
