using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace NovaFE.Infrastructure.Persistence.Sql;

internal static class SqlPersistenceExtensions
{
    /// <summary>
    /// Registra la sesión de base de datos que comparten los repositorios Dapper.
    /// <para>
    /// No registra <c>IUnitOfWork</c>: cuando EF Core también está presente, la
    /// unidad de trabajo es la de EF (dueña de las escrituras) y Dapper se usa
    /// solo para lecturas. <c>InfrastructureService</c> decide cuál aplica.
    /// </para>
    /// </summary>
    internal static IServiceCollection AddSqlPersistence(this IServiceCollection services)
    {
        // Las columnas son snake_case (created_at, legal_name); los DTOs son
        // PascalCase. Esto le dice a Dapper que ignore los guiones bajos al mapear.
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        // Npgsql devuelve DateTime para timestamptz; los modelos de lectura usan
        // DateTimeOffset. Este handler cierra la brecha.
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());

        // Se registra el tipo concreto y la interfaz apunta a la misma instancia,
        // para que DapperUnitOfWork pueda publicar la transacción en la sesión.
        services.AddScoped<DbSession>();
        services.AddScoped<IDbSession>(sp => sp.GetRequiredService<DbSession>());

        return services;
    }
}
