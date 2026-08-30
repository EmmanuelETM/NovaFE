# Multi-tenancy: esquema compartido + Row-Level Security

NovaFE es una sola instancia que atiende a muchos contribuyentes. Cada
contribuyente es un **tenant**. La inmensa mayoría de las tablas son de un tenant;
el aislamiento se garantiza en **tres capas**, ninguna suficiente por sí sola.

## 1. El contrato de dominio

Una entidad con datos de un cliente implementa `ITenantOwned`
(`src/Domain/Common/Entities/ITenantOwned.cs`):

```csharp
public sealed class Certificate : Entity<Guid>, ITenantOwned, IAuditableEntity
{
    public Guid TenantId { get; private set; }   // lo asigna la infraestructura
    // ...
}
```

`Tenant` **no** es `ITenantOwned` — es la raíz. Vive en el esquema compartido y
lo administra el operador del SaaS.

## 2. La resolución del tenant en cada petición

`ICurrentTenant` (Application) expone el tenant de la petición. Lo llena
`TenantResolutionMiddleware` (Service). **Hoy** lee el header `X-Tenant-Id`;
**mañana**, cuando exista autenticación por API key, lo resolverá de la key sin
tocar nada más — igual que `ICurrentUser` ya funciona sin autenticación.

Fuera de una petición con tenant (health checks, endpoints de operador como
`TenantsController`, jobs de fondo) `ICurrentTenant.TenantId` es `null`.

## 3. Las tres capas de aislamiento

### Capa A — Filtro global de consulta de EF Core (siempre activo)

`AppDbContext.ApplyGlobalQueryFilters` añade a cada entidad `ITenantOwned` el
filtro con nombre `"Tenant"`:

```
e => EF.Property<Guid>(e, "TenantId") == CurrentTenantId
```

`CurrentTenantId` es un miembro del contexto, así que EF lo re-evalúa en cada
consulta. Si no hay tenant, `CurrentTenantId` es `null` y **ninguna** fila
`ITenantOwned` es visible. Para saltárselo a propósito:
`query.IgnoreQueryFilters(["Tenant"])`.

Esta capa funciona **en todos los entornos**, incluido local/pruebas donde la app
se conecta como superusuario.

### Capa B — Interceptor de escritura (`TenantStampingInterceptor`)

En cada `SaveChanges`:

- estampa `TenantId` en las entidades `ITenantOwned` nuevas a partir de
  `ICurrentTenant.Require()` (lanza si no hay tenant);
- rechaza cualquier entidad cuyo `TenantId` no sea el de la petición en curso.

### Capa C — Row-Level Security en PostgreSQL (defensa en producción)

`TenantConnectionInterceptor` (EF) y `DbSession` (Dapper) ejecutan en cada
apertura de conexión:

```sql
SELECT set_config('app.tenant_id', '<uuid o cadena vacía>', false)
```

Cada migración de una tabla `ITenantOwned` llama a
`RowLevelSecurity.Enable(migrationBuilder, "nombre_tabla")`, que emite:

```sql
ALTER TABLE "x" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "x" FORCE ROW LEVEL SECURITY;
CREATE POLICY "tenant_isolation" ON "x"
    USING      (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
```

> **Un rol superusuario de PostgreSQL ignora RLS, siempre.** En local y en las
> pruebas de integración la app se conecta como `postgres`, así que ahí el
> aislamiento real lo da la Capa A. RLS es la red en **producción**, donde la app
> se conecta con un rol restringido (abajo).

## El rol de aplicación en producción

Para que RLS surta efecto, la app **no** debe conectarse como superusuario ni
como dueño de las tablas, ni tener `BYPASSRLS`:

```sql
CREATE ROLE novafe_app LOGIN PASSWORD '...';

-- Acceso a datos, nunca DDL:
GRANT CONNECT ON DATABASE novafe TO novafe_app;
GRANT USAGE ON SCHEMA public TO novafe_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO novafe_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO novafe_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO novafe_app;
```

Las migraciones las corre un rol con privilegios (el dueño), aparte del rol de
runtime. `FORCE ROW LEVEL SECURITY` hace que ni el dueño se salte la política, así
que las tareas de mantenimiento que necesiten ver todo deben fijar `app.tenant_id`
o usar un rol con `BYPASSRLS` explícito y auditado.

## Cómo agregar una entidad tenant-scoped

1. `class Foo : Entity<Guid>, ITenantOwned, IAuditableEntity` con
   `public Guid TenantId { get; private set; }`.
2. En su `IEntityTypeConfiguration`: índice sobre `TenantId` (y normalmente
   compuesto con las columnas que más se consultan).
3. En la migración de creación:
   `RowLevelSecurity.Enable(migrationBuilder, "foos");` en `Up`,
   `RowLevelSecurity.Disable(migrationBuilder, "foos");` en `Down`.
4. Las lecturas Dapper de esa tabla añaden `WHERE tenant_id = @tenantId` como
   defensa en profundidad (equivalente a la Capa A de EF).
5. Prueba de integración: dos tenants, cada uno solo ve lo suyo.
