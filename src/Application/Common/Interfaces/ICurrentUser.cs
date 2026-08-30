namespace NovaFE.Application.Common.Interfaces;

/// <summary>
/// Usuario que ejecuta la operación actual. Es la costura que permite agregar
/// autenticación más adelante <b>sin tocar los casos de uso</b>: hoy devuelve
/// valores vacíos con <see cref="IsAuthenticated"/> en false, y el día que se
/// configure JWT empieza a devolver datos reales sin cambiar nada más.
/// <para>
/// Fuera de una petición HTTP (jobs, migraciones, pruebas) todo viene en null.
/// </para>
/// </summary>
public interface ICurrentUser
{
    /// <summary>Identificador del usuario, o null si no hay usuario autenticado.</summary>
    string? Id { get; }

    /// <summary>Nombre de usuario, o null si no hay usuario autenticado.</summary>
    string? UserName { get; }

    bool IsAuthenticated { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsInRole(string role);
}
