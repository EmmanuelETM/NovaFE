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

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            options.UseNpgsql(db.ConnectionString, npgsql =>
            {
                npgsql.CommandTimeout(db.CommandTimeoutSeconds);

                if (db.MaxRetryCount > 0)
                    npgsql.EnableRetryOnFailure(db.MaxRetryCount);
            });

            options.EnableDetailedErrors(db.EnableDetailedErrors);
            options.EnableSensitiveDataLogging(db.EnableSensitiveDataLogging);

            // El orden importa: primero se traduce el borrado a lógico y después
            // se sella la auditoría, para que el registro borrado quede con UpdatedBy.
            options.AddInterceptors(
                sp.GetRequiredService<SoftDeleteInterceptor>(),
                sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        return services;
    }
}
