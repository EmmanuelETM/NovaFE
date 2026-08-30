namespace NovaFE.Application.Common.Interfaces;

/// <summary>
/// Carga datos base en la base de datos al arrancar el servicio (catálogos,
/// tenant de demo, etc.). Se ejecuta después de aplicar las migraciones y solo si
/// <c>Database:MigrateOnStartup</c> está activo.
/// <para>
/// Un seeder <b>debe ser idempotente</b>: comprueba si el dato ya existe antes de
/// insertarlo. Puede correr en cada arranque.
/// </para>
/// </summary>
public interface IDataSeeder
{
    /// <summary>Orden de ejecución relativo a los demás seeders (menor primero).</summary>
    int Order { get; }

    Task SeedAsync(CancellationToken ct = default);
}
