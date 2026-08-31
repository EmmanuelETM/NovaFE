using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Infrastructure.Caching;
using NovaFE.Infrastructure.Certificates.EfCore;
using NovaFE.Infrastructure.Certificates.Sql;
using Dapper;
using NovaFE.Infrastructure.Dgii;
using NovaFE.Infrastructure.Ecf;
using NovaFE.Infrastructure.Ecf.EfCore;
using NovaFE.Infrastructure.Ecf.Outbox;
using NovaFE.Infrastructure.Ecf.Sql;
using NovaFE.Infrastructure.Http;
using NovaFE.Infrastructure.Persistence;
using NovaFE.Infrastructure.Persistence.EfCore;
using NovaFE.Infrastructure.Persistence.Idempotency;
using NovaFE.Infrastructure.Persistence.Sql;
using NovaFE.Infrastructure.Security;
using NovaFE.Infrastructure.Sequences.EfCore;
using NovaFE.Infrastructure.Sequences.Sql;
using NovaFE.Infrastructure.Signing;
using NovaFE.Infrastructure.Tenants.EfCore;
using NovaFE.Infrastructure.Tenants.Sql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        // Generación y validación del XML del e-CF (Módulo 2). Sin estado.
        services.AddSingleton<IEcfXmlSerializer, EcfXmlSerializer>();
        services.AddSingleton<IRfceSerializer, RfceSerializer>();
        services.AddSingleton<IEcfXsdValidator, EcfXsdValidator>();

        // ==========================================
        //        Autenticación con la DGII
        // ==========================================
        services.AddOptions<DgiiOptions>()
            .Bind(configuration.GetSection(DgiiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // La BaseAddress se resuelve al crear el cliente, no aquí: así los tests
        // (y las variables de entorno) pueden sobreescribir Dgii:EcfBaseUrl.
        services.AddResilientHttpClient<IDgiiAuthClient, DgiiAuthClient>(
            sp =>
            {
                var options = sp.GetRequiredService<IOptions<DgiiOptions>>().Value;
                return new Uri(options.EcfBaseUrl.TrimEnd('/') + "/");
            },
            sp => TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<DgiiOptions>>().Value.AuthTimeoutSeconds));

        services.AddSingleton<DgiiTokenGate>();
        services.AddScoped<IDgiiTokenCache, DistributedCacheDgiiTokenCache>();
        services.AddScoped<IDgiiTokenProvider, DgiiTokenProvider>();

        // Recepción y consulta de resultado (Módulo 4): dos clientes resilientes con
        // nombre, uno por dominio de la DGII (e-CF y Facturas de Consumo).
        AddDgiiSubmissionHttpClient(services, DgiiSubmissionClient.EcfClientName, options => options.EcfBaseUrl);
        AddDgiiSubmissionHttpClient(services, DgiiSubmissionClient.FcClientName, options => options.FcBaseUrl);
        services.AddScoped<IDgiiSubmissionClient, DgiiSubmissionClient>();

        // ==========================================
        //             Repositorios
        // ==========================================
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantReadRepository, TenantReadRepository>();
        services.AddScoped<IEmitterProfileRepository, EmitterProfileRepository>();
        services.AddScoped<IEmitterProfileReadRepository, EmitterProfileReadRepository>();
        services.AddScoped<ICertificateRepository, CertificateRepository>();
        services.AddScoped<ICertificateReadRepository, CertificateReadRepository>();
        services.AddScoped<INcfSequenceRepository, NcfSequenceRepository>();
        services.AddScoped<INcfSequenceReadRepository, NcfSequenceReadRepository>();
        services.AddScoped<INcfSequenceAllocator, NcfSequenceAllocator>();
        services.AddScoped<IEcfRepository, EcfRepository>();
        services.AddScoped<IEcfReadRepository, EcfReadRepository>();
        services.AddScoped<IEcfSubmissionQueue, PostgresEcfSubmissionQueue>();
        services.AddScoped<IIdempotencyStore, PostgresIdempotencyStore>();

        // El jsonb de totales del comprobante emitido → EcfTotalsSnapshot en las lecturas Dapper.
        SqlMapper.AddTypeHandler(new EcfTotalsSnapshotJsonHandler());

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

    private static void AddDgiiSubmissionHttpClient(
        IServiceCollection services, string name, Func<DgiiOptions, string> baseUrl)
    {
        // La BaseAddress se resuelve al crear el cliente (no al registrarlo), igual
        // que el cliente de autenticación: así los tests y las variables de entorno
        // pueden sobreescribir Dgii:EcfBaseUrl / Dgii:FcBaseUrl.
        services.AddHttpClient(name, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<DgiiOptions>>().Value;
                client.BaseAddress = new Uri(baseUrl(options).TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.SubmissionTimeoutSeconds);
            })
            .AddStandardResilienceHandler();
    }
}
