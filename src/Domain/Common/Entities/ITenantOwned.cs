namespace NovaFE.Domain.Common.Entities;

/// <summary>
/// Marca una entidad que pertenece a exactamente un tenant (un contribuyente).
/// La inmensa mayoría de las tablas del dominio son de este tipo.
/// <para>
/// El aislamiento entre tenants se garantiza en tres capas:
/// </para>
/// <list type="number">
/// <item>
/// PostgreSQL aplica una política de <c>ROW LEVEL SECURITY</c> sobre cada tabla
/// así marcada, filtrando por la variable de sesión <c>app.tenant_id</c>.
/// </item>
/// <item>
/// EF Core estampa <see cref="TenantId"/> al insertar (interceptor) y añade un
/// filtro global de consulta, de modo que el aislamiento se mantiene aunque una
/// conexión llegue a la base sin la variable de sesión (p. ej. un superusuario,
/// que ignora RLS).
/// </item>
/// <item>
/// El interceptor rechaza escrituras cuyo <see cref="TenantId"/> no coincida con
/// el tenant de la petición en curso.
/// </item>
/// </list>
/// <para>
/// <see cref="TenantId"/> es de solo lectura en el dominio: lo asigna la
/// infraestructura, nunca la lógica de negocio.
/// </para>
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
