namespace NovaFE.Application.Tenants.Interfaces;

/// <summary>
/// Resuelve un token de API key presentado en una petición a la identidad del
/// contribuyente. Es la costura que llama el handler de autenticación de la capa
/// Service; la implementación (que toca la base) vive en Infrastructure.
/// </summary>
public interface IApiKeyAuthenticator
{
    /// <summary>
    /// La identidad detrás de <paramref name="presentedToken"/>, o <c>null</c> si
    /// el token no existe, está revocado o venció.
    /// </summary>
    Task<ApiKeyIdentity?> AuthenticateAsync(string presentedToken, CancellationToken ct = default);
}

/// <summary>Identidad resuelta a partir de una API key válida.</summary>
/// <param name="Environment">El nombre del ambiente de la DGII al que ata la key (<c>Test</c> / <c>Cert</c> / <c>Production</c>).</param>
public sealed record ApiKeyIdentity(Guid KeyId, Guid TenantId, string Environment);
