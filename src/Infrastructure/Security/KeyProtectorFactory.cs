using NovaFE.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace NovaFE.Infrastructure.Security;

/// <summary>
/// Construye el <see cref="IKeyProtector"/> que corresponde a un
/// <see cref="CertificateVaultOptions"/>. Lo usan el registro de DI
/// (<c>InfrastructureService</c>) y el modo de re-envoltura de secretos
/// (cambio de proveedor de KEK), que necesita instanciar dos a la vez.
/// </summary>
public static class KeyProtectorFactory
{
    public static IKeyProtector Create(CertificateVaultOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var wrapped = Options.Create(options);

        return options.Provider?.Trim().ToLowerInvariant() switch
        {
            CertificateVaultOptions.ProviderAzureKeyVault => new AzureKeyVaultKeyProtector(wrapped),
            _ => new LocalKeyProtector(wrapped),
        };
    }
}
