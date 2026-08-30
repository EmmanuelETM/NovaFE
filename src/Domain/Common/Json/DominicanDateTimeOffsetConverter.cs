using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NovaFE.Domain.Common.Json;

/// <summary>
/// La API serializa todo <see cref="DateTimeOffset"/> en <b>hora dominicana</b>
/// (offset -04:00). El instante no cambia; solo cómo se ve. Es coherente con la
/// Representación Impresa (que es legalmente hora dominicana) y evita que el
/// front tenga que convertir. Al leer acepta cualquier offset o <c>Z</c>.
/// </summary>
public sealed class DominicanDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    // 'F' (mayúscula): omite los decimales de segundo si son cero.
    private const string WriteFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(
            DominicanTimeZone.ToLocal(value).ToString(WriteFormat, CultureInfo.InvariantCulture));
    }
}
