using System.Globalization;

namespace NovaFE.Domain.Common;

/// <summary>
/// La zona horaria de República Dominicana: <c>America/Santo_Domingo</c>, UTC-4
/// fijo (el país no usa horario de verano desde el año 2000).
/// <para>
/// El almacenamiento y la lógica interna siempre son instantes UTC. Esta zona se
/// usa solo en los <b>bordes</b>: lo que se serializa hacia la DGII, la
/// Representación Impresa, y lo que ve el usuario. También la aritmética de
/// fechas de calendario (vencimiento de secuencias e-NCF, la regla de los 30
/// días de las notas de crédito, los relojes de contingencia) se hace en fecha
/// local dominicana, no en UTC.
/// </para>
/// </summary>
public static class DominicanTimeZone
{
    /// <summary>Formato de fecha y hora dominicano (y de la DGII): <c>dd-MM-yyyy HH:mm:ss</c>.</summary>
    public const string DateTimeFormat = "dd-MM-yyyy HH:mm:ss";

    /// <summary>Formato de fecha dominicano (y de la DGII): <c>dd-MM-yyyy</c>.</summary>
    public const string DateFormat = "dd-MM-yyyy";

    public static TimeZoneInfo Zone { get; } = Resolve();

    /// <summary>El mismo instante, expresado en hora local dominicana (offset -04:00).</summary>
    public static DateTimeOffset ToLocal(DateTimeOffset instant)
        => TimeZoneInfo.ConvertTime(instant, Zone);

    /// <summary>La fecha del calendario dominicano en la que cae el instante.</summary>
    public static DateOnly LocalDate(DateTimeOffset instant)
        => DateOnly.FromDateTime(ToLocal(instant).DateTime);

    /// <summary>Formatea el instante como <c>dd-MM-yyyy HH:mm:ss</c> en hora dominicana.</summary>
    public static string ToDateTimeString(DateTimeOffset instant)
        => ToLocal(instant).ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    /// <summary>Formatea el instante como <c>dd-MM-yyyy</c> en hora dominicana.</summary>
    public static string ToDateString(DateTimeOffset instant)
        => ToLocal(instant).ToString(DateFormat, CultureInfo.InvariantCulture);

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in (string[])["America/Santo_Domingo", "SA Western Standard Time"])
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Probar el siguiente identificador.
            }
        }

        // Sin base de datos de zonas horarias en el contenedor: UTC-4 fijo.
        return TimeZoneInfo.CreateCustomTimeZone(
            id: "America/Santo_Domingo",
            baseUtcOffset: TimeSpan.FromHours(-4),
            displayName: "(UTC-04:00) Santo Domingo",
            standardDisplayName: "Atlantic Standard Time");
    }
}
