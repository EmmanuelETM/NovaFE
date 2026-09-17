# Multi-tenancy jerárquico (Organization → Tenant/Project)

Backlog vivo del refactor `User -> Organization -> Tenant` (el prompt original lo
llamaba `User -> Organization -> Project`; `Tenant` **es** el "Project", por
eso la tabla de membresía se llama `tenant_members`, no `project_members`).
Estado al 2026-09-17: **Fase 1 y Fase 2 completas** (compilan, migraciones
probadas contra Postgres real en Testcontainers, 677 unitarias + 202 de
integración en verde). Ver "Estado de la base de datos" más abajo — **la
rama `dev` de Neon ya tiene el esquema de Fase 1 aplicado** (sin querer,
detalle abajo); Fase 2 se sumó ahí como migración nueva hacia adelante.

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
3. **Billing vs. fiscal**: la bolsa de comprobantes (métrica de facturación de
   NovaFE) se consolida a nivel `Organization` (`Organization.Plan`, Fase 2).
   Las secuencias e-NCF (`NcfSequence`) y los certificados `.p12` siguen
   aislados por `Tenant` — requisito regulatorio de la DGII (un rango de
   secuencia es válido para un RNC específico), no negociable ni pooleable
   entre tenants de una misma organización.

No se usa Supabase Auth ni `auth.uid()` en ningún punto de este diseño — el
proyecto usa Better Auth self-hosted + `X-Internal-Key`/`PlatformUser`, y RLS
por `app.tenant_id` (variable de sesión, no una función atada a un JWT de
Supabase).

## Fase 1 — Base de datos & dominio — HECHO

### Esquema

- `organizations` — agrupa tenants. Campos: `name`, `slug` (único),
  `plan` (`OrganizationPlan`, movido de `Tenant` en Fase 2), `status`
  (`OrganizationStatus`: `Active`/`Suspended`). Sin RLS (no es dato de un tenant).
- `organization_members` — N:M `platform_users` ↔ `organizations`, con
  `role` (`OrganizationRole`). Único por `(organization_id, platform_user_id)`.
- `tenant_members` — N:M `platform_users` ↔ `tenants`, con `role`
  (`PlatformRole`, nunca `admin_sistema`). Único por `(tenant_id, platform_user_id)`.
  Entidad de dominio `TenantMember` en `src/Domain/Tenants/` (se llamó
  `ProjectMember` en un borrador y se renombró por cohesión con la decisión
  de mantener `Tenant`, no `Project`).
- `tenants.organization_id` — nullable (transición), FK a `organizations`.
  `tenants.plan` se eliminó (Fase 2 — ver `Organization.Plan`).

Migraciones: `AddOrganizations` (esquema Fase 1) → `BackfillOrganizationsFromTenants`
(datos, idempotente) → `MoveTenantPlanToOrganization` (Fase 2: agrega
`organizations.plan`/`status`, quita `tenants.plan`).

**Backfill**: por cada tenant sin organización, crea una `Organization` 1:1
(nombre = razón social, slug derivado + sufijo del id), la asocia al tenant,
y espeja cada `platform_users` de ese tenant a `organization_members` (el
`admin_tenant` del tenant queda como `owner`, el resto como `member`) y a
`tenant_members` (mismo rol que tenía en `PlatformUser.Role`).

### Módulo `Organizations` (vertical slice completo)

`src/Domain/Organizations`, `src/Application/Organizations`,
`src/Infrastructure/Organizations`, `src/Service/Controllers/OrganizationsController.cs`.

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
| GET | `/api/v1/organizations/{id}/tenants` | Operador |

## Fase 2 — Autorización + reparto operador/cliente — HECHO

Principio rector: **el operador nunca es cuello de botella para la operación
fiscal diaria del cliente.** Solo gobierna infraestructura, facturación y
gobernanza; todo lo que toca DGII o la integración del cliente con su propio
software es self-service.

### Lo que solo hace el operador

- **Planes y cuotas**: `TenantPlan` → `Organization.Plan`. `RegisterTenantCommand`
  ya no pide plan; se factura y administra a nivel organización.
- **Suspensión**: `Tenant.Suspend()`/`Activate()` y `Organization.Suspend()`/`Activate()`
  ahora tienen idempotencia estricta (ErrorOr, como `PlatformUser.Revoke`) y
  **se hacen cumplir de verdad** — antes el flag existía pero nada lo revisaba:
  - `ApiKeyAuthenticator`: la key deja de autenticar si el tenant o su
    organización dueña están suspendidos (`ApiKeyReadRepository.FindByHashAsync`
    ahora cruza `tenants`/`organizations`).
  - `InternalKeyAuthenticationHandler`/`PlatformUserAuthenticator`: mismo
    chequeo, vía la resolución de tenant/rol de más abajo.
  - Endpoints: `POST /tenants/{id}/suspend|activate`,
    `POST /organizations/{id}/suspend|activate` — suspender la organización
    bloquea en cascada a todos sus tenants sin tocarlos uno por uno.
- **Onboarding estructural**: `POST /organizations` con `ownerEmail` opcional
  da de alta el `PlatformUser` (si no existe, vía el nuevo
  `PlatformUser.CreateOrganizationMember` — sin tenant fijo) y lo agrega como
  `owner`, atómico. Crear/asociar un `Tenant` sigue siendo del operador.
- **God mode (impersonar, etc.)**: pospuesto a Fase 5 — ver esa sección.

### Lo que el cliente hace self-service, sin el operador

- Certificados, secuencias e-NCF, perfil fiscal, webhooks: ya lo eran antes
  de esta fase (`TenantConfig`), no se tocaron.
- **API keys** (era el bug real: vivían operator-only dentro de `TenantsController`):
  se movieron a `ApiKeysController` propio — `POST/GET /api/v1/api-keys`,
  `DELETE /api/v1/api-keys/{id}` bajo `TenantConfig` (self-service), más las
  rutas `~/tenants/{id}/api-keys` de operador para soporte, mismo patrón dual
  que `CertificatesController`.
- **Gestión de miembros de organización**: `owner`/`admin` de esa organización
  puntual pueden invitar, listar, cambiar rol y quitar miembros sin operador.
  Implementado como chequeo **dentro del caso de uso**
  (`Application.Organizations.OrganizationAccess`), no como policy declarativa
  de ASP.NET — el rol de organización varía por organización (no es un claim
  fijo del principal como `tenant_id`), así que hace falta resolver la
  membresía puntual contra el id de la ruta. El controller solo exige
  `Authenticated` (cualquier principal logueado); el caso de uso decide.
  Protecciones extra: no se puede quitar ni degradar al último `owner`
  (`OrganizationErrors.CannotRemoveLastOwner`).
- **Reintentar un webhook atascado / reactivar un endpoint**: quedó **fuera de
  alcance de esta pasada** (no se construyó) — señalado como pendiente real,
  no falso "ya está". Ver "Pendiente" abajo.

### El corte de autenticación humana (el corazón de esta fase)

`PlatformUser.TenantId`/`Role` dejan de ser la fuente de verdad para un
usuario de contribuyente (siguen existiendo en la tabla, ahora vestigiales
para ese caso — ver `PlatformUser.CreateOrganizationMember`). El tenant/rol
efectivo se resuelve en cada login:

- `InternalKeyAuthenticationHandler` acepta `X-Acting-Tenant-Id` (solo
  esquema `InternalKey` — una API key ya trae su tenant implícito).
- `PlatformUserAuthenticator`/`IPlatformUserReadRepository.ResolveTenantAccessAsync`:
  si viene el header, resuelve ese tenant puntual — directo por
  `tenant_members`, o heredado como `admin_tenant` si el usuario es
  `owner`/`admin` de la organización dueña, sin fila explícita. Si no tiene
  acceso (o está suspendido), la autenticación **falla** (no cae a un default
  silencioso — pidió un tenant puntual y no le corresponde).
- Sin el header (`ResolveDefaultTenantAccessAsync`): toma el primer tenant
  accesible por antigüedad (directo, luego heredado). Si no hay ninguno
  todavía, el login **igual funciona** con `TenantId = null` — un usuario
  recién invitado a una organización sin tenants no debe quedar sin poder
  entrar al dashboard.
- `GetCurrentUserUseCase`/`UserProfileDto` ganó `Organizations` (lista de
  `{ organizationId, organizationName, role, tenants: [{ tenantId, tenantName, role }] }`)
  para el switcher de Fase 3, **sin quitar** `TenantId`/`Role`/`TenantName` a
  nivel raíz — el dashboard actual (`web/`) sigue funcionando sin cambios
  hasta que la Fase 3 consuma el array nuevo.
- Efecto colateral necesario: `ProvisionTenantUserUseCase` y
  `ChangeUserRoleUseCase` ahora escriben también en `tenant_members` (antes
  solo tocaban `PlatformUser`) — sin esto, un empleado recién dado de alta
  quedaba con identidad pero sin acceso a ningún tenant.

### Ambiente (Test/Cert/Production): no es un estado del Tenant

No existe ni debería existir un "mover el tenant a producción". El ambiente
es una propiedad de cada artefacto por separado (`Certificate.Environment`,
`NcfSequence.Environment`, `ApiKey.Environment`), y los tres conviven a la
vez para el mismo tenant. `EmitterProfile.DefaultEnvironment` es solo un
*default*, no un gate. "Pasar a producción" = sumar los artefactos de
producción sin tocar los de test.

### Onboarding de un cliente, de punta a punta (post-Fase 2)

1. Operador: `POST /organizations` con `ownerEmail` (crea org + usuario + membresía owner).
2. Operador: `POST /api/v1/tenants` (RNC, razón social) + `POST /organizations/{id}/tenants/{tenantId}`.
3. El owner entra al dashboard (Better Auth) y, self-service, sin operador:
   perfil fiscal, certificado, secuencia, primera API key, invitar al equipo.

## Fase 4 — Hardening (parcialmente hecho)

Hecho en esta pasada: pruebas de integración para suspensión (tenant y
organización, cascada), `X-Acting-Tenant-Id` (switch entre tenants, y
rechazo si no hay acceso), y self-service de miembros (owner puede,
member no). Pendiente real:

- Retry/reactivación de webhooks (self-service + operador) — no construido.
- `docs/multi-tenancy.md`, `docs/human-auth.md`, `docs/api-auth.md` todavía
  no mencionan la jerarquía Organization — desactualizados desde Fase 1.
- Decidir si un `OrgAdmin` podrá crear un `Tenant` nuevo self-service cuando
  su plan tenga cupo (abierto, se retoma con el diseño de cuotas).

## Fase 5 — Herramientas de soporte del operador (pospuesta)

Impersonar (login-as) un usuario para troubleshooting. Empezar por una
versión de **solo lectura** antes de escritura (así lo hicieron Stripe/GitHub).
Requiere un claim `impersonated_by` + reforzar que `audit_log` (RF-14.4)
registre ambas identidades. No priorizado sin clientes reales en producción.

## Fase 3 — Frontend (Next.js) — pendiente

- Regenerar `schema.d.ts` contra la API ya cambiada en Fase 2.
- Rutas `[orgSlug]/[tenantSlug]/...` en vez del árbol plano actual bajo
  `(app)/*` (`Tenant` no tiene `Slug` todavía).
- Extender `nav-tenant.tsx` de "menú de un tenant" a switcher real org→tenant,
  consumiendo `UserProfileDto.Organizations`.
- Mandar `X-Acting-Tenant-Id` desde `identityHeaders()` cuando el usuario
  elige tenant activo.

## Estado de la base de datos

**Importante — leer antes de tocar migraciones de este refactor de nuevo:**
la rama `dev` del proyecto de Neon (`withered-bonus-39380900`, branch
`br-cold-grass-a5480d8z` — la que apunta el connection string de
user-secrets) **ya tiene aplicadas** `AddOrganizations` y
`BackfillOrganizationsFromTenants`, con datos reales de prueba (backfill
corrido). Esto pasó solo, probablemente por `Database:MigrateOnStartup: true`
+ algún `dotnet run` local mientras esos archivos ya existían en el repo — no
por un `dotnet ef database update` explícito. La rama `production` de Neon
está limpia (nunca tuvo estas tablas).

Por eso `MoveTenantPlanToOrganization` (Fase 2) es una migración **nueva
hacia adelante** en vez de una regeneración de las anteriores — con
`defaultValue` explícito (`Developer`/`Active`) para no romper las filas que
ya existen en `dev`. **No** vuelvas a intentar `migrations remove` sobre
`AddOrganizations`/`BackfillOrganizationsFromTenants`: la CLI se va a negar
(correctamente) porque hay una base real detrás.

Para aplicar `MoveTenantPlanToOrganization` a `dev`:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet dotnet-ef database update \
  --project src/Infrastructure --startup-project src/Service
```
