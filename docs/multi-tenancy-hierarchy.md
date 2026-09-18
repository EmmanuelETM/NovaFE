# Multi-tenancy jerárquico (Organization → Tenant/Project)

Refactor `User -> Organization -> Tenant` (el prompt original lo llamaba
`User -> Organization -> Project`; `Tenant` **es** el "Project", por eso la
tabla de membresía se llama `tenant_members`, no `project_members`).

**Estado al 2026-09-18: Fases 1, 2, 3, 4 y 5 completas.** Backend y frontend en
verde (`typecheck`/`lint`/`format:check` limpios). La Fase 5 (consola real de
operador, incluida impersonación de solo lectura) ya no está pospuesta — se
construyó completa (backend + frontend) el 2026-09-17. Ver "Pendiente /
abierto" al final — quedan dos cosas reales sin cerrar, ninguna bloquea el uso
normal del sistema.

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
- `tenants.organization_id` — FK a `organizations`, **nullable**
  (`Tenant.OrganizationId`, `Guid?`). Un tenant puede quedar sin organización:
  `Tenant.UnassignFromOrganization()` lo desasocia explícitamente (operador,
  `DELETE /organizations/{id}/tenants/{tenantId}`, con su propio audit log —
  ver tabla de endpoints más abajo). `tenants.plan` se eliminó (Fase 2 — ver
  `Organization.Plan`).

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
| PATCH | `/api/v1/organizations/{id}/plan` | Operador (Fase 5 — sin pasarela de pago, corrección manual) |
| POST | `/api/v1/organizations/{id}/members` | **Self-service**: `owner`/`admin` de esa organización, o el operador |
| GET | `/api/v1/organizations/{id}/members` | **Self-service**: cualquier miembro de esa organización, o el operador |
| PATCH | `/api/v1/organizations/{id}/members/{userId}` | **Self-service**: `owner`/`admin`, o el operador |
| DELETE | `/api/v1/organizations/{id}/members/{userId}` | **Self-service**: `owner`/`admin`, o el operador |
| POST | `/api/v1/organizations/{id}/tenants/{tenantId}` | Operador (crear/asociar tenants sigue siendo estructural) |
| DELETE | `/api/v1/organizations/{id}/tenants/{tenantId}` | Operador (desasociar; `Tenant.OrganizationId` queda `null`, con audit log dedicado) |
| GET | `/api/v1/organizations/{id}/tenants` | **Self-service**: cualquier miembro de esa organización, o el operador (abierto en el rediseño de UI, ver Fase 4) |
| GET | `/api/v1/organizations/{id}/audit-log` | Operador (Fase 5) |

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
- **Herramientas de soporte** (impersonar, etc.): construidas en Fase 5 — ver
  esa sección más abajo.

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
  alimenta el grid de `/org/[orgSlug]` (Fase 4).
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
  resuelto). Es la fuente que consume el switcher del dashboard (Fase 4).
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

## Fase 3 — Reestructuración de rutas (Next.js) — HECHO

Rutas **planas**, no anidadas — decisión confirmada comparando explícitamente
contra Supabase real (`/dashboard/project/<ref>` no vive bajo `/org/<slug>/`):

- `/org/[orgSlug]` — gestión de la organización: miembros, plan. Páginas
  nuevas sobre los endpoints self-service que ya dejó Fase 2, sin tocar el
  backend.
- `/tenant/[tenantId]` — el espacio de trabajo del contribuyente:
  comprobantes, certificados, secuencias, webhooks, empresa, configuración.
- Sin prefijo de organización en la ruta del tenant porque `tenantId` (un
  GUID) ya es único y estable aunque el tenant cambie de nombre o de
  organización.

**Identidad**: `identityHeaders(tenantId?)` (`web/lib/api/server.ts`) manda
`X-Acting-Tenant-Id` cuando hay sesión y se pasa un `tenantId` explícito.
Del lado del cliente, `X-Active-Tenant-Id` es una pista (no una credencial)
que el proxy (`app/api/backend/[...path]/route.ts`) valida como GUID antes
de convertirla en el header real — `SKIPPED_HEADERS` bloquea que el
navegador inyecte `x-acting-tenant-id` directo.

**Switcher (primera versión)**: `nav-tenant.tsx`, un Command+Popover
agrupado por organización — reemplazado en Fase 4 por
`OrgSwitcher`/`TenantSwitcher`. La cookie `last_tenant_id` solo decide a
dónde aterriza la raíz `/` la próxima vez; la URL sigue siendo la fuente de
verdad del tenant activo.

`UserProfileDto` gana `OrganizationSlug`/`Plan`/`Status` por organización.

## Fase 4 — Rediseño de UI/UX del dashboard, estándar Supabase — HECHO

Construida el mismo día que Fase 3 (2026-09-17, commit `43100e0`). Fase 3
reestructuró las rutas; Fase 4 rediseñó el shell y la navegación por encima
de esas rutas.

- **Un solo shell compartido** (`AppSidebarProvider` + `AppSidebar` +
  `AppTopbar`) para los scopes tenant/organización/operador, en vez de que
  `/org/[orgSlug]` tuviera su propio layout con pestañas planas.
- **`OrgSwitcher`/`TenantSwitcher`** en la barra superior, encadenados
  (Organización ▾ / Tenant ▾), patrón "split button" — reemplazan
  `NavTenant` de Fase 3 (mezclaba cambiar de contexto con administración, un
  antipatrón). El nombre es un enlace directo a la organización o al
  tenant, el chevron aparte abre el combobox de cambio. Badge de plan y de
  ambiente DGII (Test/Cert/Production). En mobile se reemplazan por un
  único trigger que abre un `Drawer` con pestañas Tenant/Organización (el
  par de switchers no entra en una pantalla angosta). Cambiar de
  tenant/organización siempre navega — nunca hay una cookie que decida qué
  datos se piden dentro de una pantalla ya cargada.
- **Sidebar de tenant categorizado** en cuatro grupos (Operativo /
  Configuración fiscal / Desarrolladores e integración / Ajustes).
  Clientes/Receptores y Logs de auditoría quedaron marcados "pronto"
  (`ready: false`) a propósito. El pie del sidebar queda únicamente con
  `NavUser` (se movió del topbar).
- **Paleta de comandos** (`⌘K`/`Ctrl+K`): navegación pura entre pantallas y
  tenants/organizaciones, sin buscador de datos — no hay backend de
  búsqueda todavía. Único uso de Zustand del proyecto (si la paleta está
  abierta o no).
- **API Keys self-service completo** en el frontend (el backend ya existía
  desde Fase 2).
- **Grid de tenants** en `/org/[orgSlug]` + página nueva `/organizaciones`
  — listado global de las organizaciones del usuario, fuera del shell de
  sidebar (es un salto de contexto, no una pantalla dentro de un tenant o
  de una organización puntual).
- Backend: `GET /organizations/{id}/tenants` pasa de operador a
  self-service (mismo patrón `OrganizationAccess.CanViewAsync` que
  `ListOrganizationMembers`), con prueba de integración nueva (miembro vs.
  no miembro).
- Fix: `members-screen.tsx` formateaba `createdAt` (`DateTimeOffset`) con
  el helper de `DateOnly` — causaba un `RangeError` al abrir
  `/org/[slug]/miembros`.

Detalle completo, incluidos los componentes exactos, en la memoria del
proyecto (`multi-tenancy-hierarchy-fase1.md`, sesión de Claude Code).

## Fase 5 — Consola real de operador (Nemus Admin) — HECHO

Construida el 2026-09-17, backend y frontend, a pedido explícito: el
self-service del lado cliente ya cerraba, pero el operador de NovaFE no tenía
forma de gestionar la jerarquía Organization → Tenant desde el dashboard.

### Backend (5 piezas)

1. **Cambiar el plan de una organización**: `Organization.ChangePlan(...)`
   (sin invariante que romper, no hay pasarela de pago todavía) +
   `PATCH /api/v1/organizations/{id}/plan`.
2. **Webhooks, patrón dual operador**: `WebhooksController` pasó el
   `[Authorize]` de nivel de clase a nivel de acción — ASP.NET Core combina
   `[Authorize]` de clase + método con **AND**, no OR, así que con la clase en
   `TenantConfig` un operador real habría fallado igual. Rutas
   `...ForTenant` vía `currentTenant.Set(tenantId)` + delegar, mismo patrón
   que `ApiKeysController`.
3. **Vista cross-tenant de entregas muertas**: `GET /api/v1/ops/dead-deliveries`,
   sin filtro de tenant a propósito (`webhook_deliveries` es tabla de sistema
   sin RLS).
4. **Impersonación de solo lectura**: header `X-Impersonate-User-Id`, solo
   esquema `InternalKey`, solo si el actor ya autenticó como
   `admin_sistema`. `InternalKeyAuthenticationHandler` autentica al operador
   real primero y, si pide impersonar, reemplaza los claims publicados por
   los del usuario impersonado más `impersonated_by` con el id del operador
   real (rechaza impersonar a otro operador). `ImpersonationReadOnlyFilter`
   (filtro global en `Program.cs`) bloquea con 403 cualquier
   POST/PUT/PATCH/DELETE mientras ese claim esté presente — un solo punto de
   corte, no hace falta tocar cada caso de uso. `audit_log` gana la columna
   `impersonated_by` (migración `AddImpersonatedByToAuditLog`): la fila queda
   con `Actor` = usuario impersonado, `ImpersonatedBy` = operador real.
5. **Columna de organización en `GET /tenants`**: `TenantSummaryDto` suma
   `OrganizationId`/`OrganizationName`/`OrganizationSlug`.

### Frontend — `/nemus/*` (dentro del route group `(app)`)

- `/nemus/organizaciones` — lista + detalle (plan editable inline,
  suspender/reactivar, tabs Miembros/Tenants reusando las pantallas
  self-service).
- Columna "Organización" + tabs Webhooks/Auditoría en `/nemus/tenants/{id}`,
  con componentes y hooks separados de los self-service (el self-service
  depende de `useTenantId()`, que lee el segmento `[tenantId]`; la ruta de
  operador usa `[id]`).
- `DeadDeliveriesCard` cross-tenant en `/nemus/operacion`.
- Búsqueda en el `⌘K`/`Ctrl+K`, solo en scope operador, con grupos
  "Contribuyentes"/"Organizaciones".
- Impersonación: acción "Ver como" en la gestión de usuarios (solo filas de
  empleado de contribuyente activo, nunca operador). Setea una cookie
  httpOnly de 30 min que `identityHeaders()` traduce a
  `X-Impersonate-User-Id`. Cambia la sesión completa del operador mientras
  dura — no es una vista superpuesta: `GET /users/me` devuelve el usuario
  impersonado y el layout pasa a ser el de tenant. `ImpersonationBanner`
  montado en los tres layouts (tenant/organización/operador) por si la
  cookie sigue viva al navegar entre scopes.

## Pendiente / abierto

Todo lo demás de este refactor está cerrado. Lo que sigue sin resolver:

1. ~~**Documentación desactualizada**~~ — **hecho.** `docs/human-auth.md`,
   `docs/multi-tenancy.md` y `docs/api-auth.md` reconciliados contra el
   código actual (verificado, no copiado de una versión anterior del doc).
2. **Decisión de producto abierta**: ¿un `OrgAdmin` podrá crear un `Tenant`
   nuevo self-service cuando su plan tenga cupo? Hoy crear/asociar un tenant
   sigue siendo estructural (operador). Se retoma cuando se diseñe el motor
   de cuotas por plan.
3. **Impersonación de escritura**: Fase 5 solo cubrió lectura a propósito
   (mismo criterio que Stripe/GitHub). Pasar a escritura, si hace falta, es
   un slice posterior.

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
