using System.Text.RegularExpressions;

namespace NovaFE.Service.Common;

/// <summary>
/// Convierte los tokens <c>[controller]</c> y <c>[action]</c> de las rutas a
/// <c>kebab-case</c> en minúsculas: <c>EcfPreview</c> → <c>ecf-preview</c>,
/// <c>Tenants</c> → <c>tenants</c>. Se aplica a todos los controllers (presentes y
/// futuros) vía <see cref="Microsoft.AspNetCore.Mvc.ApplicationModels.RouteTokenTransformerConvention"/>,
/// para que las URLs sigan la convención estándar sin repetir rutas explícitas.
/// </summary>
internal sealed partial class KebabCaseParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value)
    {
        if (value is null)
            return null;

        var text = value.ToString();
        return string.IsNullOrEmpty(text)
            ? text
            : CamelBoundary().Replace(text, "$1-$2").ToLowerInvariant();
    }

    // "EcfPreview" → "Ecf-Preview"; "RFCEDoc" → "RFCE-Doc".
    [GeneratedRegex(@"([a-z0-9])([A-Z])")]
    private static partial Regex CamelBoundary();
}
