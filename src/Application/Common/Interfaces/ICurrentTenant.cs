namespace NovaFE.Application.Common.Interfaces;

/// <summary>
/// El tenant para el que corre la operación actual. Lo llena la capa Service a
/// partir de la petición (hoy: header <c>X-Tenant-Id</c>; más adelante: la API
/// key). Es la costura que mantiene a los casos de uso libres de
/// <c>HttpContext</c>, igual que <see cref="ICurrentUser"/>.
/// <para>
/// Fuera de una petición con tenant —health checks, directorio de DGII,
/// endpoints de operador, jobs de fondo— <see cref="TenantId"/> es null.
/// </para>
/// </summary>
public interface ICurrentTenant
{
    /// <summary>Id del tenant actual, o null si la operación no está asociada a uno.</summary>
    Guid? TenantId { get; }

    bool HasValue { get; }

    /// <summary>
    /// El id del tenant actual; lanza <see cref="InvalidOperationException"/> si no
    /// hay ninguno. Úsalo en rutas de código que no deben correr sin tenant.
    /// </summary>
    Guid Require();
}
