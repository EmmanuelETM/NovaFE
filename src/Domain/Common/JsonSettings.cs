using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NovaFE.Domain.Common.Json;

public static class JsonSettings
{
    // Una única instancia estática y de solo lectura para toda la aplicación.
    // Esto es vital porque instanciar JsonSerializerOptions es costoso en memoria.
    public static readonly JsonSerializerOptions Bulletproof = new()
    {
        // 1. Ignora mayúsculas/minúsculas ("Codigo" vs "codigo")
        PropertyNameCaseInsensitive = true,

        // 2. Convierte todo a camelCase por defecto al serializar
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        // 3. No envíes propiedades que sean null (reduce el tamaño del payload y ahorra ancho de banda)
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // 4. Permite leer números que vienen como strings ("1" -> 1)
        NumberHandling = JsonNumberHandling.AllowReadingFromString,

        Converters =
        {
            // Convierte los Enums a texto legible en lugar de números enteros
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),

            // Serializa todas las fechas en hora dominicana (offset -04:00).
            new DominicanDateTimeOffsetConverter(),
        }
    };
}

/// <summary>
/// Lee un valor JSON no textual (número o booleano) como <see cref="string"/>.
/// Útil para APIs externas que envían el mismo campo unas veces como 0 y otras como "0".
/// <para>
/// Es <b>opt-in a propósito</b>: aplícalo con un atributo sobre la propiedad concreta.
/// Registrarlo de forma global interceptaría TODA propiedad string de la aplicación,
/// lo que oculta payloads mal formados en lugar de rechazarlos.
/// </para>
/// <example>
/// <code>
/// public record RespuestaExterna(
///     [property: JsonConverter(typeof(NumberToStringConverter))] string Codigo);
/// </code>
/// </example>
/// </summary>
public class NumberToStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() ?? string.Empty;

            case JsonTokenType.Number:
                // Se cubre todo el rango numérico, no solo Int32: un long o un decimal
                // que no cupiera en int provocaría una excepción en tiempo de ejecución.
                if (reader.TryGetInt64(out var entero))
                    return entero.ToString(CultureInfo.InvariantCulture);

                return reader.GetDecimal().ToString(CultureInfo.InvariantCulture);

            case JsonTokenType.True:
            case JsonTokenType.False:
                return reader.GetBoolean().ToString(CultureInfo.InvariantCulture);

            default:
                throw new JsonException($"No se puede convertir {reader.TokenType} a string.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
