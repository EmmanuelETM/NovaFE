using System.Globalization;
using System.Xml;

namespace NovaFE.Infrastructure.Ecf;

/// <summary>
/// Envoltura fina sobre <see cref="XmlWriter"/> con los verbos de emisión del e-CF:
/// elemento obligatorio (<see cref="El(string,string)"/>), opcional
/// (<see cref="Opt(string,string?)"/>), monto (<see cref="Money"/> /
/// <see cref="MoneyOpt"/>) y ámbito de elemento (<see cref="Element"/>, con
/// <c>using</c>). El código de serialización queda leyéndose como el árbol del XSD.
/// <para>
/// El formateo de montos se inyecta (<paramref name="moneyFormat"/>): el
/// <c>&lt;ECF&gt;</c> usa <see cref="EcfXmlFormat.Money"/> (1–2 decimales); el
/// <c>&lt;RFCE&gt;</c>, <see cref="EcfXmlFormat.Money2"/> (exactamente 2). Fechas:
/// <see cref="EcfXmlFormat.Date"/>.
/// </para>
/// </summary>
internal readonly struct EcfElementWriter(XmlWriter writer, Func<decimal, string>? moneyFormat = null)
{
    private readonly Func<decimal, string> _money = moneyFormat ?? EcfXmlFormat.Money;

    /// <summary>Abre un elemento; el <c>using</c> lo cierra.</summary>
    public Scope Element(string name)
    {
        writer.WriteStartElement(name);
        return new Scope(writer);
    }

    /// <summary>Elemento obligatorio con valor de texto (ya seguro para XML).</summary>
    public void El(string name, string value) => writer.WriteElementString(name, value);

    /// <summary>Elemento obligatorio con valor entero (cultura invariante).</summary>
    public void El(string name, int value) => writer.WriteElementString(name, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Elemento opcional de texto: se omite si es null/vacío; se hace <c>Trim</c>.</summary>
    public void Opt(string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            writer.WriteElementString(name, value.Trim());
    }

    /// <summary>Elemento opcional entero: se omite si es null.</summary>
    public void Opt(string name, int? value)
    {
        if (value is { } v)
            El(name, v);
    }

    /// <summary>Elemento opcional de fecha (<c>dd-MM-yyyy</c>): se omite si es null.</summary>
    public void Opt(string name, DateOnly? value)
    {
        if (value is { } v)
            El(name, EcfXmlFormat.Date(v));
    }

    /// <summary>Monto obligatorio (formato inyectado en el constructor).</summary>
    public void Money(string name, decimal value) => El(name, _money(value));

    /// <summary>
    /// Monto opcional: se omite si es null o ≤ 0. La mayoría de estos campos del XSD
    /// son "mayor que cero" y un 0 significa "no aplica".
    /// </summary>
    public void MoneyOpt(string name, decimal? value)
    {
        if (value is { } v and > 0m)
            El(name, _money(v));
    }

    /// <summary>Ámbito de elemento — cierra el elemento al hacer <c>Dispose</c>.</summary>
    internal readonly struct Scope(XmlWriter writer) : IDisposable
    {
        public void Dispose() => writer.WriteEndElement();
    }
}
