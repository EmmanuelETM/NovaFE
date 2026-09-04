using NovaFE.Domain.Common;

namespace NovaFE.Application.Common.Interfaces;

/// <summary>
/// El tenant para el que corre la operación actual. Lo llena la capa Service a
/// partir de la petición (el claim <c>tenant_id</c> del principal: de la API key,
/// o del header <c>X-Tenant-Id</c> en Development). Es la costura que mantiene a
/// los casos de uso libres de <c>HttpContext</c>, igual que <see cref="ICurrentUser"/>.
/// <para>
/// Fuera de una petición con tenant —health checks, directorio de DGII,
/// endpoints de operador, jobs de fondo— <see cref="TenantId"/> es null.
/// </para>
/// </summary>
public interface ICurrentTenant
{
    /// <summary>Id del tenant actual, o null si la operación no está asociada a uno.</summary>
    Guid? TenantId { get; }

    /// <summary>
    /// El ambiente de la DGII que trae la credencial de la petición (la API key
    /// lo lleva). <c>null</c> si la petición no lo especifica — el camino
    /// <c>X-Tenant-Id</c> de Development, o cualquier contexto sin key.
    /// </summary>
    DgiiEnvironment? Environment { get; }

    bool HasValue { get; }

    /// <summary>
    /// El id del tenant actual; lanza <see cref="InvalidOperationException"/> si no
    /// hay ninguno. Úsalo en rutas de código que no deben correr sin tenant.
    /// </summary>
    Guid Require();
}
