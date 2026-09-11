using NovaFE.Application.Audit.Interfaces;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Ecf.Representation;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Infrastructure.Audit.Sql;
using NovaFE.Infrastructure.Caching;
using NovaFE.Infrastructure.Notifications;
using NovaFE.Application.Notifications;
using NovaFE.Infrastructure.Certificates.EfCore;
using NovaFE.Infrastructure.Certificates.Sql;
using Dapper;
using NovaFE.Infrastructure.Dgii;
using NovaFE.Infrastructure.Ecf;
using NovaFE.Infrastructure.Ecf.EfCore;
using NovaFE.Infrastructure.Ecf.Outbox;
using NovaFE.Infrastructure.Ecf.Representation;
using NovaFE.Infrastructure.Ecf.Sql;
using NovaFE.Infrastructure.Http;
using NovaFE.Infrastructure.Persistence;
using NovaFE.Infrastructure.Persistence.Audit;
using NovaFE.Infrastructure.Persistence.EfCore;
using NovaFE.Infrastructure.Representation;
using NovaFE.Infrastructure.Persistence.Idempotency;
using NovaFE.Infrastructure.Persistence.Sql;
using NovaFE.Infrastructure.Security;
using NovaFE.Infrastructure.Sequences.EfCore;
using NovaFE.Infrastructure.Sequences.Sql;
using NovaFE.Infrastructure.Settings;
using NovaFE.Infrastructure.Settings.EfCore;
using NovaFE.Infrastructure.Settings.Sql;
using NovaFE.Infrastructure.Signing;
using NovaFE.Infrastructure.Tenants;
using NovaFE.Infrastructure.Tenants.EfCore;
using NovaFE.Infrastructure.Tenants.Sql;
using NovaFE.Infrastructure.Users;
using NovaFE.Infrastructure.Users.EfCore;
using NovaFE.Infrastructure.Users.Sql;
using NovaFE.Infrastructure.Webhooks;
using NovaFE.Infrastructure.Webhooks.EfCore;
using NovaFE.Infrastructure.Webhooks.Outbox;
using NovaFE.Infrastructure.Webhooks.Sql;
using NovaFE.Application.Webhooks;
using NovaFE.Application.Webhooks.Interfaces;
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
            {
                // El connection string se toma de su lugar estándar y se normaliza
                // acá una sola vez: pool acotado, timeouts, TLS y el reset de
                // sesión que necesita el aislamiento por tenant. EF, Dapper y los
                // health checks consumen este mismo valor. Ver DatabaseConnectionString.
                var raw = configuration.GetConnectionString(DatabaseOptions.ConnectionName) ?? string.Empty;
                options.ConnectionString = string.IsNullOrWhiteSpace(raw)
                    ? string.Empty
                    : DatabaseConnectionString.Normalize(raw, options);
            })
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
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CertificateVaultOptions>, CertificateVaultOptionsValidator>();

        // El proveedor de IKeyProtector se elige por configuración: 'local' (KEK en
        // config) o 'azure-key-vault' (la KEK vive en el vault). Cambiar de uno a
        // otro es solo CertificateVault:Provider — sin tocar código. Ver
        // docs/certificates.md. Se resuelve desde IOptions (no un snapshot de
        // IConfiguration) para ver la configuración final.
        services.AddSingleton<IKeyProtector>(sp =>
            KeyProtectorFactory.Create(sp.GetRequiredService<IOptions<CertificateVaultOptions>>().Value));

        services.AddScoped<ICertificateVault, EnvelopeCertificateVault>();

        // Re-envuelve la DEK de cada secreto de certificado al cambiar de proveedor
        // de KEK (la DEK y el ciphertext no cambian, solo la KEK que la protege).
        // Operación de una sola vez; ver docs/certificates.md.
        services.AddScoped<CertificateSecretRewrapper>();

        // Firma XMLDSig (parámetros exactos de la DGII). Sin estado → singleton.
        services.AddSingleton<IXmlSigner, XmlDsigSigner>();

        // Generación y validación del XML del e-CF (Módulo 2). Sin estado.
        services.AddSingleton<IEcfXmlSerializer, EcfXmlSerializer>();
        services.AddSingleton<IRfceSerializer, RfceSerializer>();
        services.AddSingleton<IEcfXsdValidator, EcfXsdValidator>();

        // Representación Impresa (Módulo 9): lee el <ECF> firmado → modelo → PDF.
        services.AddSingleton<IEcfRepresentationReader, EcfXmlRepresentationReader>();
        services.AddSingleton<IRepresentationRenderer, QuestPdfRepresentationRenderer>();

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
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IApiKeyReadRepository, ApiKeyReadRepository>();
        services.AddScoped<IApiKeyAuthenticator, ApiKeyAuthenticator>();
        services.AddScoped<IPlatformUserRepository, PlatformUserRepository>();
        services.AddScoped<IPlatformUserReadRepository, PlatformUserReadRepository>();
        services.AddScoped<IPlatformUserAuthenticator, PlatformUserAuthenticator>();
        services.AddScoped<ICertificateRepository, CertificateRepository>();
        services.AddScoped<ICertificateReadRepository, CertificateReadRepository>();
        services.AddScoped<INcfSequenceRepository, NcfSequenceRepository>();
        services.AddScoped<INcfSequenceReadRepository, NcfSequenceReadRepository>();
        services.AddScoped<INcfSequenceAllocator, NcfSequenceAllocator>();
        services.AddScoped<IEcfRepository, EcfRepository>();
        services.AddScoped<IEcfReadRepository, EcfReadRepository>();
        services.AddScoped<IEcfSubmissionQueue, PostgresEcfSubmissionQueue>();
        services.AddScoped<IIdempotencyStore, PostgresIdempotencyStore>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuditLogReadRepository, AuditLogReadRepository>();
        services.AddScoped<IExpiryNotificationLog, PostgresExpiryNotificationLog>();
        services.AddScoped<IWebhookEndpointRepository, WebhookEndpointRepository>();
        services.AddScoped<IWebhookEndpointReadRepository, WebhookEndpointReadRepository>();
        services.AddScoped<IWebhookDeliveryReadRepository, WebhookDeliveryReadRepository>();
        services.AddScoped<IWebhookOutbox, PostgresWebhookOutbox>();

        // Settings de plataforma (motor de configuración runtime, ver
        // docs/configuration.md). EF escribe, Dapper lee. El snapshot en memoria,
        // el lector tipado y el invalidador se registran más abajo.
        services.AddScoped<IPlatformSettingRepository, PlatformSettingRepository>();
        services.AddScoped<IPlatformSettingReadRepository, PlatformSettingReadRepository>();
        services.AddScoped<IPlatformSettingChangeReadRepository, PlatformSettingChangeReadRepository>();
        services.AddScoped<IPlatformSettingChangeLog, PlatformSettingChangeLog>();
        services.AddScoped<ISettingsGenerationStore, SettingsGenerationStore>();
        services.AddScoped<ISettingsSnapshotLoader, SettingsSnapshotLoader>();
        services.AddScoped<ISettingsCacheInvalidator, SettingsCacheInvalidator>();
        // Snapshot en memoria + lector tipado: sin estado por petición → singleton.
        services.AddSingleton<ISettingsSnapshotHolder, SettingsSnapshotHolder>();
        services.AddSingleton<ISettingsReader, CachedSettingsReader>();

        // Settings de tenant (scope Tenant, tabla tenant_settings con RLS). El
        // lector vive por scope: lee las filas del tenant bajo demanda y cae a la
        // capa de plataforma. No hay snapshot ni bump de generación (ver docs).
        services.AddScoped<ITenantSettingRepository, TenantSettingRepository>();
        services.AddScoped<ITenantSettingReadRepository, TenantSettingReadRepository>();
        services.AddScoped<ITenantSettingChangeLog, TenantSettingChangeLog>();
        services.AddScoped<ITenantSettingsReader, ScopedTenantSettingsReader>();
        // Al arrancar, avisa de overrides huérfanos o corruptos que la resolución ignora.
        services.AddHostedService<SettingsStartupAudit>();

        // Firma HMAC: sin estado → singleton. El guard anti-SSRF (resuelve DNS)
        // depende de WebhookSettings, que es transient sobre IOptionsMonitor para
        // recoger cambios en caliente; por eso este también es transient.
        services.AddTransient<IWebhookUrlPolicy, HttpWebhookUrlPolicy>();
        services.AddSingleton<IWebhookSignature, HmacWebhookSignature>();

        // Cliente de entrega: timeout corto, SIN reintento de Polly (el outbox reintenta).
        services.AddHttpClient<IWebhookSender, HttpWebhookSender>((sp, client) =>
            client.Timeout = sp.GetRequiredService<WebhookSettings>().DeliveryTimeout);

        // Los jsonb del comprobante emitido → tipos de dominio en las lecturas Dapper.
        SqlMapper.AddTypeHandler(new EcfTotalsSnapshotJsonHandler());
        SqlMapper.AddTypeHandler(new DgiiMessagesJsonHandler());

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
