using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore;
using NovaFE.Infrastructure.Security;
using NovaFE.IntegrationTests.Fixtures;
using NovaFE.Service.Common;

namespace NovaFE.IntegrationTests.Security;

/// <summary>
/// Cambiar de proveedor de KEK re-envuelve la DEK de cada secreto, sin tocar la
/// DEK ni el ciphertext. Si esto corrompiera una fila, un certificado quedaría
/// irrecuperable — de ahí la prueba.
/// </summary>
public sealed class CertificateSecretRewrapTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private static CertificateVaultOptions Local(string masterKey) => new()
    {
        Provider = CertificateVaultOptions.ProviderLocal,
        MasterKey = masterKey,
    };

    // El IKeyProtector que la app usa hoy (lo que sea que haya ganado en config).
    private static IKeyProtector AppProtector(IServiceProvider sp)
        => KeyProtectorFactory.Create(sp.GetRequiredService<IOptions<CertificateVaultOptions>>().Value);

    [RequiresDockerFact]
    public async Task Rewrapping_to_a_new_kek_keeps_the_secret_recoverable_with_that_kek()
    {
        var tenant = Guid.CreateVersion7();
        var pkcs12 = RandomNumberGenerator.GetBytes(512);
        const string password = "cert-pw-ñ";

        // 1. Guardar un secreto con la KEK de la app.
        string reference;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenant);
            var vault = scope.ServiceProvider.GetRequiredService<ICertificateVault>();
            reference = await vault.StoreAsync(pkcs12, password);
        }

        var newMasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var to = KeyProtectorFactory.Create(Local(newMasterKey));

        // 2. Re-envolver old → new.
        int rewrapped;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenant);
            rewrapped = await scope.ServiceProvider
                .GetRequiredService<CertificateSecretRewrapper>()
                .RewrapCurrentTenantAsync(AppProtector(scope.ServiceProvider), to, NullLogger.Instance);
        }

        rewrapped.ShouldBe(1);

        // 3. La KEK vieja ya no lo abre.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenant);
            var oldVault = scope.ServiceProvider.GetRequiredService<ICertificateVault>();
            await Should.ThrowAsync<CryptographicException>(() => oldVault.RetrieveAsync(reference));
        }

        // 4. La KEK nueva sí, y devuelve el PKCS#12 y la contraseña originales.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenant);
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var newVault = new EnvelopeCertificateVault(context, to, TimeProvider.System);

            using var secret = await newVault.RetrieveAsync(reference);
            secret.Pkcs12.ShouldBe(pkcs12);
            secret.Password.ShouldBe(password);
        }
    }

    [RequiresDockerFact]
    public async Task Rewrapping_with_the_wrong_source_kek_throws_and_changes_nothing()
    {
        var tenant = Guid.CreateVersion7();

        string reference;
        byte[] wrappedKeyBefore;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenant);
            var vault = scope.ServiceProvider.GetRequiredService<ICertificateVault>();
            reference = await vault.StoreAsync(Encoding.UTF8.GetBytes("pfx"), "pw");

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            wrappedKeyBefore = context.Set<CertificateSecretRow>()
                .Single(r => r.Reference == Guid.Parse(reference)).WrappedKey;
        }

        var wrongFrom = KeyProtectorFactory.Create(Local(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
        var to = KeyProtectorFactory.Create(Local(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenant);
            var rewrapper = scope.ServiceProvider.GetRequiredService<CertificateSecretRewrapper>();

            await Should.ThrowAsync<CryptographicException>(
                () => rewrapper.RewrapCurrentTenantAsync(wrongFrom, to, NullLogger.Instance));
        }

        // La fila quedó intacta: SaveChanges nunca corrió.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenant);
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var after = context.Set<CertificateSecretRow>()
                .Single(r => r.Reference == Guid.Parse(reference)).WrappedKey;
            after.ShouldBe(wrappedKeyBefore);
        }
    }
}
