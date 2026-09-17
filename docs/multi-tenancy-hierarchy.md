# Multi-tenancy jerárquico (Organization → Tenant/Project)

Refactor `User -> Organization -> Tenant` (el prompt original lo llamaba
`User -> Organization -> Project`; `Tenant` **es** el "Project", por eso la
tabla de membresía se llama `tenant_members`, no `project_members`).

**Estado al 2026-09-17: Fases 1, 2 y 3 completas.** Backend: 677 unitarias +
207 de integración en verde. Frontend: `typecheck`/`lint`/`format:check`
limpios. Fase 5 (impersonación de soporte) sigue pospuesta a propósito, sin
clientes reales en producción todavía. Ver "Pendiente / abierto" al final —
hay tres cosas reales sin cerrar, ninguna bloquea el uso normal del sistema.

## Decisiones de Fase 0 (confirmadas)

1. **Naming**: se mantiene `Tenant` en código y BD — no se renombra a
   `Project`. Conceptualmente, `Tenant` es el Proyecto/RNC/Sucursal.
2. **Roles**: dos jerarquías **ortogonales**, no una sola:
   - `OrganizationRole` (`owner`/`admin`/`member`) — nivel organización:
     facturación, invitar miembros, crear/asociar tenants.
   - `PlatformRole`/`ApiKeyRole` (`admin_tenant`/`emisor`/`consultor`,
     ya existían) — nivel tenant/proyecto: configuración fiscal, emisión,
     consulta. Un `member` de organización puede ser `admin_tenant` de uno de
     sus tenants sin ser `owner`/`admin` de la organización.
3. **Facturación vs. fiscal**: la bolsa de comprobantes (métrica de
   facturación de NovaFE) se consolida a nivel `Organization`
   (`Organization.Plan`, Fase 2). Las secuencias e-NCF (`NcfSequence`) y los
   certificados `.p12` siguen aislados por `Tenant` — requisito regulatorio
   de la DGII (un rango de secuencia es válido para un RNC específico), no
   negociable ni combinable entre tenants de una misma organización.

No se usa Supabase Auth ni `auth.uid()` en ningún punto de este diseño — el
proyecto usa Better Auth self-hosted + `X-Internal-Key`/`PlatformUser`, y RLS
por `app.tenant_id` (variable de sesión, no una función atada a un JWT de
Supabase).

## Fase 1 — Base de datos y dominio — HECHO

### Esquema

- `organizations` — agrupa tenants. Campos: `name`, `slug` (único),
  `plan` (`OrganizationPlan`, movido de `Tenant` en Fase 2), `status`
  (`OrganizationStatus`: `Active`/`Suspended`). Sin RLS (no es dato de un tenant).
- `organization_members` — N a N entre `platform_users` y `organizations`,
  con `role` (`OrganizationRole`). Único por `(organization_id, platform_user_id)`.
- `tenant_members` — N a N entre `platform_users` y `tenants`, con `role`
  (`PlatformRole`, nunca `admin_sistema`). Único por `(tenant_id, platform_user_id)`.
  Entidad de dominio `TenantMember` en `src/Domain/Tenants/` (se llamó
  `ProjectMember` en un borrador y se renombró por cohesión con la decisión
  de mantener `Tenant`, no `Project`).
- `tenants.organization_id` — FK a `organizations`. `tenants.plan` se
  eliminó (Fase 2 — ver `Organization.Plan`).

Migraciones: `AddOrganizations` (esquema Fase 1) → `BackfillOrganizationsFromTenants`
(datos, idempotente) → `MoveTenantPlanToOrganization` (Fase 2: agrega
`organizations.plan`/`status`, quita `tenants.plan`).

**Backfill**: por cada tenant sin organización, crea una `Organization` 1 a 1
(nombre = razón social, slug derivado más un sufijo del id), la asocia al
tenant, y refleja cada `platform_users` de ese tenant en `organization_members`
(el `admin_tenant` del tenant queda como `owner`, el resto como `member`) y en
`tenant_members` (mismo rol que tenía en `PlatformUser.Role`).

### Módulo `Organizations` (vertical slice completo)

`src/Domain/Organizations`, `src/Application/Organizations`,
`src/Infrastructure/Organizations`, `src/Service/Controllers/OrganizationsController.cs`.

## Fase 2 — Autorización y reparto operador/cliente — HECHO

Principio rector: **el operador nunca es un cuello de botella para la
operación fiscal diaria del cliente.** Solo gobierna infraestructura,
facturación y gobernanza; todo lo que toca la DGII o la integración del
cliente con su propio software es self-service.

### Endpoints de `Organizations`

| Método | Ruta | Quién |
|---|---|---|
| POST | `/api/v1/organizations` | Operador (`ownerEmail` opcional: onboarding atómico) |
| GET | `/api/v1/organizations/{id}` | Operador |
| GET | `/api/v1/organizations` | Operador |
| POST | `/api/v1/organizations/{id}/suspend` | Operador |
| POST | `/api/v1/organizations/{id}/activate` | Operador |
| POST | `/api/v1/organizations/{id}/members` | **Self-service**: `owner`/`admin` de esa organización, o el operador |
| GET | `/api/v1/organizations/{id}/members` | **Self-service**: cualquier miembro de esa organización, o el operador |
| PATCH | `/api/v1/organizations/{id}/members/{userId}` | **Self-service**: `owner`/`admin`, o el operador |
| DELETE | `/api/v1/organizations/{id}/members/{userId}` | **Self-service**: `owner`/`admin`, o el operador |
| POST | `/api/v1/organizations/{id}/tenants/{tenantId}` | Operador (crear/asociar tenants sigue siendo estructural) |
| GET | `/api/v1/organizations/{id}/tenants` | **Self-service**: cualquier miembro de esa organización, o el operador (abierto en la reestructuración de UI, ver Fase 3) |

### Lo que solo hace el operador

- **Planes y cuotas**: `TenantPlan` pasó a `Organization.Plan`.
  `RegisterTenantCommand` ya no pide plan; se factura y administra a nivel
  organización.
- **Suspensión**: `Tenant.Suspend()`/`Activate()` y
  `Organization.Suspend()`/`Activate()` tienen idempotencia estricta
  (`ErrorOr`, como `PlatformUser.Revoke`) y **se hacen cumplir de verdad**:
  - `ApiKeyAuthenticator`: la key deja de autenticar si el tenant o su
    organización dueña están suspendidos.
  - `InternalKeyAuthenticationHandler`/`PlatformUserAuthenticator`: mismo
    chequeo, vía la resolución de tenant/rol de más abajo.
  - Endpoints: `POST /tenants/{id}/suspend|activate`,
    `POST /organizations/{id}/suspend|activate` — suspender la organización
    bloquea en cascada a todos sus tenants sin tocarlos uno por uno.
- **Onboarding estructural**: `POST /organizations` con `ownerEmail`
  opcional da de alta el `PlatformUser` (si no existe, vía
  `PlatformUser.CreateOrganizationMember` — sin tenant fijo) y lo agrega
  como `owner`, todo en un solo paso. Crear/asociar un `Tenant` sigue siendo
  del operador.
- **Herramientas de soporte** (impersonar, etc.): pospuesto a Fase 5.

### Lo que el cliente hace self-service, sin el operador

- Certificados, secuencias e-NCF, perfil fiscal, webhooks, API keys, logs de
  auditoría del propio tenant: todo bajo la política `TenantConfig`.
- **Gestión de miembros de organización**: `owner`/`admin` de esa
  organización puntual pueden invitar, listar, cambiar rol y quitar
  miembros sin operador. Implementado como chequeo **dentro del caso de
  uso** (`Application.Organizations.OrganizationAccess`), no como política
  declarativa de ASP.NET — el rol de organización varía por organización
  (no es un claim fijo del principal como `tenant_id`), así que hace falta
  resolver la membresía puntual contra el id de la ruta. El controller solo
  exige `Authenticated` (cualquier principal logueado); el caso de uso
  decide. Protección extra: no se puede quitar ni degradar al último
  `owner` (`OrganizationErrors.CannotRemoveLastOwner`).
- **Listado de tenants de la organización**: cualquier miembro ve
  Razón Social/RNC/Estado de todos los tenants de su organización, aunque
  no sea `tenant_member` de cada uno puntualmente — es la lista que
  alimenta el grid de `/org/[orgSlug]` (Fase 3).
- **Reintentar una entrega de webhook muerta**: cerrado en un backlog
  posterior a esta fase — `POST /webhooks/{id}/deliveries/{deliveryId}/retry`,
  ver `docs/webhooks.md`.

### El corte de autenticación humana (el corazón de esta fase)

`PlatformUser.TenantId`/`Role` dejan de ser la fuente de verdad para un
usuario de contribuyente (siguen existiendo en la tabla, ahora vestigiales
para ese caso — ver `PlatformUser.CreateOrganizationMember`). El tenant y el
rol efectivos se resuelven en cada login:

- `InternalKeyAuthenticationHandler` acepta `X-Acting-Tenant-Id` (solo en el
  esquema `InternalKey` — una API key ya trae su tenant implícito).
- `PlatformUserAuthenticator`/`IPlatformUserReadRepository.ResolveTenantAccessAsync`:
  si viene el header, resuelve ese tenant puntual — directo por
  `tenant_members`, o heredado como `admin_tenant` si el usuario es
  `owner`/`admin` de la organización dueña, sin fila explícita. Si no tiene
  acceso (o está suspendido), la autenticación **falla** — pidió un tenant
  puntual y no le corresponde, no cae a un default silencioso.
- Sin el header (`ResolveDefaultTenantAccessAsync`): toma el primer tenant
  accesible por antigüedad (directo, luego heredado). Si no hay ninguno
  todavía, el login **igual funciona** con `TenantId = null` — un usuario
  recién invitado a una organización sin tenants no debe quedar sin poder
  entrar al dashboard.
- `GetCurrentUserUseCase`/`UserProfileDto` expone `Organizations`
  (`{ organizationId, organizationName, organizationSlug, plan, status, role, tenants: [{ tenantId, tenantName, role }] }`),
  además de `TenantId`/`Role`/`TenantName` a nivel raíz (el tenant activo ya
  resuelto). Es la fuente que consume el switcher del dashboard (Fase 3).
- Efecto colateral necesario: `ProvisionTenantUserUseCase` y
  `ChangeUserRoleUseCase` escriben también en `tenant_members` (antes solo
  tocaban `PlatformUser`) — sin esto, un empleado recién dado de alta
  quedaba con identidad pero sin acceso a ningún tenant.

### Ambiente (Test/Cert/Production): no es un estado del Tenant

No existe ni debería existir un "mover el tenant a producción". El ambiente
es una propiedad de cada artefacto por separado (`Certificate.Environment`,
`NcfSequence.Environment`, `ApiKey.Environment`), y los tres conviven a la
vez para el mismo tenant. `EmitterProfile.DefaultEnvironment` es solo un
valor por defecto, no una compuerta. "Pasar a producción" es sumar los
artefactos de producción sin tocar los de test.

### Onboarding de un cliente, de punta a punta

1. Operador: `POST /organizations` con `ownerEmail` (crea la organización, el
   usuario y la membresía `owner`).
2. Operador: `POST /api/v1/tenants` (RNC, razón social) más
   `POST /organizations/{id}/tenants/{tenantId}`.
3. El owner entra al dashboard (Better Auth) y, self-service, sin operador:
   perfil fiscal, certificado, secuencia, primera API key, invita a su
   equipo.

## Fase 3 — Frontend (Next.js), estándar Supabase — HECHO

Rutas **planas**, no anidadas — decisión confirmada comparando explícitamente
contra Supabase real (`/dashboard/project/<ref>` no vive bajo `/org/<slug>/`):

- `/org/[orgSlug]` — gestión de la organización: grid de tenants, miembros,
  plan. Sidebar propio con esas tres secciones (antes eran pestañas planas
  sin sidebar).
- `/tenant/[tenantId]` — el espacio de trabajo del contribuyente: inicio,
  comprobantes, perfil emisor, certificados, secuencias, API keys, webhooks,
  logs de auditoría, configuración. Sidebar categorizado en cuatro grupos
  (Operativo / Configuración fiscal / Desarrolladores e integración /
  Ajustes).
- Sin prefijo de organización en la ruta del tenant porque `tenantId` (un
  GUID) ya es único y estable aunque el tenant cambie de nombre o de
  organización.
- `/organizaciones` — listado global de las organizaciones del usuario,
  fuera del shell de sidebar (es un salto de contexto, no una pantalla
  dentro de un tenant o de una organización puntual).

**Identidad**: `identityHeaders()` (`web/lib/api/server.ts`) manda
`X-Acting-Tenant-Id` cuando hay sesión y se pasa un `tenantId` explícito.
Del lado del cliente, `X-Active-Tenant-Id` es una pista (no una credencial)
que el proxy (`app/api/backend/[...path]/route.ts`) valida como GUID antes
de convertirla en el header real — `SKIPPED_HEADERS` bloquea que el
navegador inyecte `x-acting-tenant-id` directo.

**Switcher**: `OrgSwitcher`/`TenantSwitcher` en la barra superior,
encadenados (Organización ▾ / Tenant ▾), patrón "split button" — el nombre
es un enlace directo a la organización o al tenant, el chevron aparte abre
el combobox de cambio. En mobile se reemplazan por un único trigger que abre
un `Drawer` con pestañas Tenant/Organización (el par de switchers no entra
en una pantalla angosta). Cambiar de tenant/organización siempre navega —
nunca hay una cookie que decida qué datos se piden dentro de una pantalla ya
cargada; la única cookie (`last_tenant_id`) solo decide a dónde aterriza la
raíz `/` la próxima vez.

**Paleta de comandos** (`⌘K`/`Ctrl+K`): navegación pura entre pantallas y
tenants/organizaciones, sin buscador de datos — no hay backend de búsqueda
todavía.

Detalle completo, incluidos los componentes exactos, en la memoria del
proyecto (`multi-tenancy-hierarchy-fase1.md`, sesión de Claude Code).

## Fase 5 — Herramientas de soporte del operador (pospuesta)

Impersonar (login-as) un usuario para resolución de problemas. Empezar por
una versión de **solo lectura** antes de escritura (así lo hicieron
Stripe/GitHub). Requiere un claim `impersonated_by` y reforzar que
`audit_log` (RF-14.4) registre ambas identidades. No priorizado sin clientes
reales en producción.

## Pendiente / abierto

Todo lo demás de este refactor está cerrado. Lo que sigue sin resolver:

1. ~~**Documentación desactualizada**~~ — **hecho.** `docs/human-auth.md`,
   `docs/multi-tenancy.md` y `docs/api-auth.md` reconciliados contra el
   código actual (verificado, no copiado de una versión anterior del doc).
2. **Decisión de producto abierta**: ¿un `OrgAdmin` podrá crear un `Tenant`
   nuevo self-service cuando su plan tenga cupo? Hoy crear/asociar un tenant
   sigue siendo estructural (operador). Se retoma cuando se diseñe el motor
   de cuotas por plan.
3. **Fase 5** (impersonación), pospuesta a propósito — ver arriba.

## Estado de la base de datos

**Importante — leer antes de tocar migraciones de este refactor de nuevo:**
la rama `dev` del proyecto de Neon (`withered-bonus-39380900`, branch
`br-cold-grass-a5480d8z` — la que apunta el connection string de
user-secrets) **ya tiene aplicadas** `AddOrganizations` y
`BackfillOrganizationsFromTenants`, con datos reales de prueba (backfill
corrido). Esto pasó solo, probablemente por `Database:MigrateOnStartup: true`
más algún `dotnet run` local mientras esos archivos ya existían en el
repo — no por un `dotnet ef database update` explícito. La rama
`production` de Neon está limpia (nunca tuvo estas tablas).

Por eso `MoveTenantPlanToOrganization` (Fase 2) es una migración **nueva
hacia adelante** en vez de una regeneración de las anteriores — con
`defaultValue` explícito (`Developer`/`Active`) para no romper las filas que
ya existen en `dev`. **No** vuelvas a intentar `migrations remove` sobre
`AddOrganizations`/`BackfillOrganizationsFromTenants`: la CLI se va a negar
(correctamente) porque hay una base real detrás.

Para aplicar una migración nueva de este refactor a `dev`:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet dotnet-ef database update \
  --project src/Infrastructure --startup-project src/Service
```
