using System.Security.Cryptography;

namespace NovaFE.Application.Webhooks;

/// <summary>
/// Genera el secret compartido de un endpoint: <c>whsec_</c> + 32 bytes de RNG
/// criptográfico en base64url. Se guarda en claro (hace falta para calcular el
/// HMAC de cada entrega) y se enseña una sola vez — al crear el endpoint y al
/// rotarlo.
/// </summary>
public static class WebhookSecret
{
    public const string Prefix = "whsec_";

    public static string Generate() =>
        Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
