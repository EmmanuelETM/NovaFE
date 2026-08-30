using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Signing;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Infrastructure.Caching;
using NovaFE.Infrastructure.Persistence;
using NovaFE.Infrastructure.Persistence.EfCore;
using NovaFE.Infrastructure.Persistence.EfCore.Repositories;
using NovaFE.Infrastructure.Persistence.Sql;
using NovaFE.Infrastructure.Persistence.Sql.Repositories;
using NovaFE.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NovaFE.Infrastructure;

public static class InfrastructureService
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // El connection string se toma de ConnectionStrings:Default y los ajustes
        // de la sección Database. ValidateOnStart hace que un connection string
        // vacío impida el arranque en lugar de fallar en el primer request.
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .PostConfigure(options =>
                options.ConnectionString =
                    configuration.GetConnectionString(DatabaseOptions.ConnectionName) ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ==========================================
        //             Persistencia
        // ==========================================
        services.AddSqlPersistence();
        // Se registra después de Dapper a propósito: cuando conviven las dos,
        // la unidad de trabajo es la de EF Core, dueña de las escrituras.
        services.AddEfCorePersistence();

        // ==========================================
        //             Caché
        // ==========================================
        // Distribuida en memoria por ahora (una sola instancia). Ver docs/redis.md
        // para pasar a Redis sin tocar a los consumidores.
        services.AddCache();

        // ==========================================
        //        Vault de certificados
        // ==========================================
        // Envelope encryption (AES-256-GCM) con el ciphertext en la base y la KEK
        // en configuración/KMS. Ver docs/certificates.md para el porqué y los
        // otros backends posibles (Supabase Vault, HashiCorp Vault).
        services.AddOptions<CertificateVaultOptions>()
            .Bind(configuration.GetSection(CertificateVaultOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IKeyProtector, LocalKeyProtector>();
        services.AddScoped<ICertificateVault, EnvelopeCertificateVault>();

        // Firma XMLDSig (parámetros exactos de la DGII). Sin estado → singleton.
        services.AddSingleton<IXmlSigner, XmlDsigSigner>();

        // ==========================================
        //             Repositorios
        // ==========================================
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantReadRepository, TenantReadRepository>();
        services.AddScoped<ICertificateRepository, CertificateRepository>();
        services.AddScoped<ICertificateReadRepository, CertificateReadRepository>();

        // ==========================================
        //         Clientes HTTP externos
        // ==========================================
        // Ejemplo con resiliencia (reintentos + circuit breaker) ya incluida:
        // services.AddHttpClient<IEcfGateway, EcfGateway>(client =>
        //     {
        //         client.BaseAddress = new Uri(configuration["EcfGateway:BaseUrl"]!);
        //     })
        //     .AddStandardResilienceHandler();

        return services;
    }
}
