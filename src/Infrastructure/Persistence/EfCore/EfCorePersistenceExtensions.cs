using NovaFE.Application.Common.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NovaFE.Infrastructure.Persistence.EfCore;

internal static class EfCorePersistenceExtensions
{
    /// <summary>
    /// Registra el contexto de EF Core, sus interceptores y la unidad de trabajo.
    /// Los repositorios concretos se registran en <c>InfrastructureService</c>.
    /// </summary>
    internal static IServiceCollection AddEfCorePersistence(this IServiceCollection services)
    {
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<TenantStampingInterceptor>();
        services.AddScoped<TenantConnectionInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            options.UseNpgsql(db.ConnectionString, npgsql =>
            {
                npgsql.CommandTimeout(db.CommandTimeoutSeconds);

                if (db.MaxRetryCount > 0)
                    npgsql.EnableRetryOnFailure(db.MaxRetryCount);
            });

            // snake_case en toda la base: es la convención de PostgreSQL y coincide
            // con el diseño del esquema del proyecto.
            options.UseSnakeCaseNamingConvention();

            options.EnableDetailedErrors(db.EnableDetailedErrors);
            options.EnableSensitiveDataLogging(db.EnableSensitiveDataLogging);

            // El orden importa:
            //   1. TenantConnection fija app.tenant_id en la conexión (RLS).
            //   2. SoftDelete traduce el borrado físico a lógico.
            //   3. TenantStamping asigna/valida TenantId en las entidades ITenantOwned.
            //   4. Auditable sella CreatedBy/UpdatedBy, para que el borrado quede con UpdatedBy.
            options.AddInterceptors(
                sp.GetRequiredService<TenantConnectionInterceptor>(),
                sp.GetRequiredService<SoftDeleteInterceptor>(),
                sp.GetRequiredService<TenantStampingInterceptor>(),
                sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        return services;
    }
}
