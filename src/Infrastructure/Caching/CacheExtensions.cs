using Microsoft.Extensions.DependencyInjection;

namespace NovaFE.Infrastructure.Caching;

internal static class CacheExtensions
{
    /// <summary>
    /// Caché distribuida. Hoy es <b>en memoria</b>: sin dependencias, sin
    /// infraestructura que desplegar ni monitorear. Suficiente mientras la API
    /// corra en una sola instancia.
    /// <para>
    /// Los consumidores dependen de <c>IDistributedCache</c> (una abstracción),
    /// no de una implementación concreta. Pasar a Redis —cuando haya varias
    /// réplicas y haga falta caché compartida— es agregar un paquete y cambiar
    /// esta línea por <c>AddStackExchangeRedisCache(...)</c>; ningún consumidor
    /// cambia. Ver <c>docs/redis.md</c>.
    /// </para>
    /// <para>
    /// <b>Ojo:</b> la caché en memoria no es durable ni compartida entre
    /// réplicas. Lo que exige durabilidad o unicidad —claves de idempotencia,
    /// lock de asignación de secuencias e-NCF— va a PostgreSQL, no aquí.
    /// </para>
    /// </summary>
    internal static IServiceCollection AddCache(this IServiceCollection services)
    {
        services.AddDistributedMemoryCache();

        return services;
    }
}
