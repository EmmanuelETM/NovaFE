using System.Security.Cryptography;
using System.Text;

namespace NovaFE.Application.Tenants.CreateApiKey;

/// <summary>
/// Genera y hashea los tokens de API key. El token en claro es
/// <c>nfe_</c> + 43 caracteres base64url (32 bytes de RNG criptográfico). Solo se
/// persiste su SHA-256 en hex, que también es la clave de búsqueda O(1) al
/// autenticar.
/// </summary>
public static class ApiKeyToken
{
    /// <summary>Prefijo de todo token, para poder distinguirlo de otros secretos.</summary>
    public const string Prefix = "nfe_";

    /// <summary>Cuántos caracteres del token se guardan en claro para mostrar en un listado.</summary>
    public const int DisplayPrefixLength = 12;

    /// <summary>Un token nuevo en claro. Solo se enseña una vez.</summary>
    public static string Generate() =>
        Prefix + Base64Url(RandomNumberGenerator.GetBytes(32));

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
