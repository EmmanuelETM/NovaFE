using NovaFE.Domain.Common;

namespace NovaFE.Application.Ecf.IssueEcf;

/// <summary>
/// Resuelve los campos "enum" del payload de emisión: aceptan el nombre
/// (<c>"credit"</c>, <c>"check_transfer"</c>, <c>"CheckTransfer"</c>) o el código
/// DGII (<c>"2"</c>). La comparación ignora mayúsculas, guiones y guiones bajos.
/// </summary>
internal static class EcfPayloadEnum
{
    public static T? Resolve<T>(string? raw) where T : Enumeration<T>
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var value = raw.Trim();

        if (int.TryParse(value, out var code))
            return Enumeration<T>.GetAll().FirstOrDefault(item => item.Id == code);

        var normalized = Normalize(value);
        return Enumeration<T>.GetAll().FirstOrDefault(item => Normalize(item.Name) == normalized);
    }

    private static string Normalize(string value)
        => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
