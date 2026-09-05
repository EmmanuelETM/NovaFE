# Registro de auditoría (Módulo 14, RF-14.4)

**Estado: implementado.** Cada petición a un endpoint `[Authorize]` (tenant o
operador) deja una fila en `audit_log`, con éxito o no. Tabla de sistema —
sin RLS, sin `ITenantOwned`, sin `UPDATE`/`DELETE` en el código de la aplicación.

## Qué registra

Exactamente lo que exige RF-14.4 más el contexto mínimo para que la fila sea
legible — **sin** cuerpo de request/response (no hace falta y evita capturar
datos fiscales o PII de más):

| Columna | Contenido |
|---|---|
| `occurred_at` | Instante UTC de la petición (`TimeProvider`) |
| `tenant_id` | El tenant, si la petición autenticó con uno; `null` si no (operador, o autenticación fallida) |
| `actor` | `apikey:{id}` / `operator` / `anonymous` |
| `actor_role` | `admin_tenant` / `emisor` / `consultor` / `admin_sistema`; `null` si no autenticó |
| `ip_address` | IP del cliente |
| `http_method`, `path` | La acción — p. ej. `POST /api/v1/ecf` |
| `status_code`, `succeeded` | El resultado |
| `trace_id` | Correlaciona con los logs estructurados de Serilog |
| `duration_ms` | Duración de la petición |

## Cómo se captura

`AuditLoggingMiddleware` (`src/Service/Middlewares/`) se registra **antes** de
`app.UseAuthorization()`, no después — a propósito. Un middleware corre el
código posterior a `await next(context)` cuando *todo* lo que sigue en el
pipeline ya terminó, así que puesto ahí ve el `StatusCode` final de la petición,
incluyendo los `401`/`403` que `UseAuthorization()` corta y que nunca llegarían a
un middleware registrado después de ella (como `TenantResolutionMiddleware`).

Por la misma razón, el `tenant_id` de la fila sale del claim `tenant_id`
directamente de `context.User` (poblado por la autenticación, que corre antes) y
**no** de `ICurrentTenant`: `TenantResolutionMiddleware` corre después de
`UseAuthorization()`, así que en un `401`/`403` nunca llega a ejecutarse, y
`ICurrentTenant.TenantId` se quedaría en `null` incluso para una API key válida a
la que solo le faltó el rol — perdiendo justo el caso que RF-14.4 más le importa
a un auditor (un acceso *denegado*).

Solo se audita si el endpoint resuelto tiene metadata `IAuthorizeData`
(`context.GetEndpoint()?.Metadata.GetMetadata<IAuthorizeData>()`) — así
`health`/`openapi`/`scalar` y los `dev/**` (anónimos, solo Development) quedan
afuera sin mantener una lista de rutas a mano.

La escritura es **síncrona** — el middleware espera el `INSERT` antes de que la
petición termine de responder, para no arriesgar perder una fila. Usa
`CancellationToken.None` a propósito: si el cliente se desconectó justo al
terminar, igual queremos que quede el registro.

## Piezas

- `src/Application/Common/AuditLogEntry.cs` + `IAuditLogWriter` — el contrato que
  usa el middleware; implementado en
  `src/Infrastructure/Persistence/Audit/AuditLogWriter.cs` con un `INSERT` crudo
  vía Dapper (mismo patrón que `PostgresIdempotencyStore`). **No** hay ningún
  método de `UPDATE`/`DELETE` en esta clase — esa ausencia es la garantía de
  inmutabilidad a nivel de aplicación.
- `src/Infrastructure/Persistence/Audit/AuditLogRow.cs` (+ `AuditLogConfiguration.cs`)
  — el modelo EF-only que solo existe para que la migración salga del modelo
  (como `IdempotencyKey`/`EcfSubmissionOutboxRow`); las lecturas y la escritura
  reales van por Dapper, no por el `DbSet`.
- Migración `AddAuditLog` — crea la tabla. Sin `RowLevelSecurity.Enable`: debe
  poder registrar acciones de operador y peticiones anónimas rechazadas, que no
  tienen tenant.

## Lectura: `GET /api/v1/tenants/{id}/audit-log`

Recurso de operador (`X-Admin-Key`), paginado (`?page=&pageSize=`, mismo
`PagedRequest`/`PagedResult` que el resto de la API). `ListAuditLogUseCase` +
`IAuditLogReadRepository` (Dapper) siguen el patrón de `ListApiKeysUseCase` /
`ListEcfUseCase`.

## Producción: inmutabilidad a nivel de base de datos

Localmente la conexión es el superusuario `postgres`
(`appsettings.Development.json`), que ignora cualquier `REVOKE` igual que ya
ignora `FORCE ROW LEVEL SECURITY` (ver `docs/multi-tenancy.md` /
`RowLevelSecurity.cs`) — por eso la migración no intenta un `REVOKE`. En
producción (Supabase), como paso de despliegue, el rol de conexión de la app
debería tener `REVOKE UPDATE, DELETE ON audit_log FROM <rol_app>` para que la
inmutabilidad no dependa solo de que el código nunca llame a esos métodos.

## Fuera de alcance (v1)

- Filtros adicionales en el listado (por acción, rango de fechas, actor) — hoy
  solo pagina por `occurred_at DESC`.
- Retención/archivado — la tabla crece sin límite; una política de purga es un
  slice de operaciones aparte.
- Auditar el cuerpo de la petición/respuesta.
- Un listado global (todos los tenants a la vez) — hoy es siempre por tenant.
