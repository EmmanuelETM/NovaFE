using NovaFE.Infrastructure.Security;
using NovaFE.Service.Common;
using Microsoft.Extensions.Options;

namespace NovaFE.Service.Maintenance;

/// <summary>
/// Modo "re-envolver y salir": al cambiar <c>CertificateVault:Provider</c> en un
/// despliegue que ya tiene certificados, la DEK de cada secreto está envuelta con
/// la KEK vieja. Este paso la re-envuelve con la nueva (la DEK y el ciphertext del
/// PKCS#12 no cambian). Se corre una vez, con la app apuntando al proveedor
/// <b>nuevo</b> y la config del <b>viejo</b> en <c>CertificateVault:Rewrap</c>.
/// <para>
/// Itera tenant por tenant fijando <see cref="CurrentTenant"/>, igual que el
/// worker de envío: así RLS y el filtro de EF acotan las filas y no hace falta un
/// rol de base con <c>BYPASSRLS</c>.
/// </para>
/// Ver <c>docs/certificates.md</c>.
/// </summary>
public static class RewrapCertificateSecrets
{
    public const string EnabledKey = "CertificateVault:Rewrap:Enabled";

    private const string FromProviderKey = "CertificateVault:Rewrap:FromProvider";
    private const string FromMasterKeyKey = "CertificateVault:Rewrap:FromMasterKey";
    private const string FromKeyVaultKeyUriKey = "CertificateVault:Rewrap:FromKeyVaultKeyUri";

    public static async Task<int> RewrapCertificateSecretsAsync(
        this WebApplication app, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("CertificateVault.Rewrap");

        var toOptions = app.Services.GetRequiredService<IOptions<CertificateVaultOptions>>().Value;
        var fromOptions = new CertificateVaultOptions
        {
            Provider = app.Configuration[FromProviderKey] ?? string.Empty,
            MasterKey = app.Configuration[FromMasterKeyKey] ?? string.Empty,
            KeyVaultKeyUri = app.Configuration[FromKeyVaultKeyUriKey] ?? string.Empty,
        };

        if (string.IsNullOrWhiteSpace(fromOptions.Provider))
            throw new InvalidOperationException(
                $"Falta {FromProviderKey}: el proveedor de KEK del que se viene.");

        var from = KeyProtectorFactory.Create(fromOptions);
        var to = KeyProtectorFactory.Create(toOptions);

        logger.LogInformation(
            "Re-envoltura de secretos de certificado: {From} → {To}.",
            fromOptions.Provider, toOptions.Provider);

        IReadOnlyList<Guid> tenantIds;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            tenantIds = await scope.ServiceProvider
                .GetRequiredService<CertificateSecretRewrapper>()
                .AllTenantIdsAsync(ct);
        }

        var total = 0;
        foreach (var tenantId in tenantIds)
        {
            await using var scope = app.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);

            total += await scope.ServiceProvider
                .GetRequiredService<CertificateSecretRewrapper>()
                .RewrapCurrentTenantAsync(from, to, logger, ct);
        }

        logger.LogInformation(
            "Re-envoltura completa: {Total} secreto(s) en {Tenants} tenant(s).",
            total, tenantIds.Count);

        return total;
    }
}
