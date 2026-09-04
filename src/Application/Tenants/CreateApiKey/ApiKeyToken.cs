using System.Security.Cryptography;
using System.Text;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Tenants.CreateApiKey;

/// <summary>
/// Genera y hashea los tokens de API key. El token en claro es
/// <c>sk_nfe_&lt;test|cert|prod&gt;_</c> + 43 caracteres base64url (32 bytes de RNG
/// criptográfico) — el segmento de ambiente hace evidente de un vistazo con qué
/// key estás trabajando. Solo se persiste su SHA-256 en hex, que también es la
/// clave de búsqueda O(1) al autenticar.
/// </summary>
public static class ApiKeyToken
{
    /// <summary>Prefijo común de todo token, para distinguirlo de otros secretos.</summary>
    public const string Prefix = "sk_nfe_";

    /// <summary>Cuántos caracteres del token se guardan en claro para mostrar en un listado.</summary>
    public const int DisplayPrefixLength = 16;

    /// <summary>Un token nuevo en claro para el ambiente dado. Solo se enseña una vez.</summary>
    public static string Generate(DgiiEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return $"{Prefix}{environment.Slug}_{Base64Url(RandomNumberGenerator.GetBytes(32))}";
    }

    /// <summary>SHA-256 del token en hex minúscula (64 caracteres).</summary>
    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
    }

    /// <summary>Los primeros caracteres del token, para reconocerlo en un listado.</summary>
    public static string DisplayPrefix(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return token.Length <= DisplayPrefixLength ? token : token[..DisplayPrefixLength];
    }

    /// <summary>Forma básica de un token bien formado (prefijo + cuerpo no vacío).</summary>
    public static bool LooksLikeToken(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith(Prefix, StringComparison.Ordinal)
        && value.Length > Prefix.Length + 20;

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
