# Multi-tenancy jerárquico (Organization → Tenant/Project)

Backlog vivo del refactor `User -> Organization -> Tenant` (el prompt original lo
llamaba `User -> Organization -> Project`; `Tenant` **es** el "Project", por
eso la tabla de membresía se llama `tenant_members`, no `project_members`).
Estado al 2026-09-17: **Fase 1 completa** (compila, migraciones probadas
contra Postgres real en Testcontainers, 676 unitarias + 193 de integración en
verde). **Ninguna migración se aplicó todavía a una base de datos real**
(ni local persistente ni Neon) — ver "Aplicar la migración" al final.

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
   NovaFE) se consolida a nivel `Organization`. Las secuencias e-NCF
   (`NcfSequence`) y los certificados `.p12` siguen aislados por `Tenant` —
   son un requisito regulatorio de la DGII (un rango de secuencia es válido
   para un RNC específico), no negociable ni pooleable entre proyectos de una
   misma organización.

No se usa Supabase Auth ni `auth.uid()` en ningún punto de este diseño — el
proyecto usa Better Auth self-hosted + `X-Internal-Key`/`PlatformUser`, y RLS
por `app.tenant_id` (variable de sesión, no una función atada a un JWT de
Supabase). Ver el análisis completo de arquitectura en el hilo que originó
este documento si hace falta el porqué en detalle.

## Fase 1 — Base de datos & dominio (.NET) — HECHO

### Esquema nuevo

- `organizations` — agrupa tenants. Sin RLS (no es dato de un tenant).
- `organization_members` — N:M `platform_users` ↔ `organizations`, con
  `role` (`OrganizationRole`). Único por `(organization_id, platform_user_id)`.
- `tenant_members` — N:M `platform_users` ↔ `tenants`, con `role`
  (`PlatformRole`, nunca `admin_sistema`). Único por `(tenant_id, platform_user_id)`.
  (Entidad de dominio: `TenantMember`, en `src/Domain/Tenants/` — se llamó
  `ProjectMember`/`project_members` en un borrador inicial y se renombró para
  no introducir "project" como término en código cuando la Fase 0 ya decidió
  que `Tenant` es el nombre que se queda.)
- `tenants.organization_id` — nullable (transición), FK a `organizations`.

Migraciones: `20260917041500_AddOrganizations` (esquema) +
`20260917041526_BackfillOrganizationsFromTenants` (datos, idempotente vía
`WHERE organization_id IS NULL` + `ON CONFLICT DO NOTHING`).

**Backfill**: por cada tenant sin organización, crea una `Organization` 1:1
(nombre = razón social, slug derivado + sufijo del id para unicidad), la
asocia al tenant, y espeja cada `platform_users` de ese tenant a
`organization_members` (el `admin_tenant` del tenant queda como `owner`, el
resto como `member`) y a `tenant_members` (mismo rol que tenía en
`PlatformUser.Role`).

### Módulo `Organizations` (vertical slice completo)

`src/Domain/Organizations`, `src/Application/Organizations`,
`src/Infrastructure/Organizations`, `src/Service/Controllers/OrganizationsController.cs`
— mismo patrón que `Tenants`. Endpoints (todos bajo la política `Operator`,
`X-Admin-Key`, igual que `TenantsController` hoy):

| Método | Ruta | Qué hace |
|---|---|---|
| POST | `/api/v1/organizations` | Registra una organización |
| GET | `/api/v1/organizations/{id}` | Detalle |
| GET | `/api/v1/organizations` | Listado paginado |
| POST | `/api/v1/organizations/{id}/members` | Agrega un miembro por correo |
| GET | `/api/v1/organizations/{id}/members` | Lista miembros |
| PATCH | `/api/v1/organizations/{id}/members/{userId}` | Cambia rol de un miembro |
| DELETE | `/api/v1/organizations/{id}/members/{userId}` | Quita un miembro |
| POST | `/api/v1/organizations/{id}/tenants/{tenantId}` | Asocia/reasocia un tenant existente |
| GET | `/api/v1/organizations/{id}/tenants` | Lista los tenants de la organización |

### `PlatformUser` — qué cambió y qué NO

**No se tocaron** `PlatformUser.TenantId`/`PlatformUser.Role` ni ningún caso de
uso que dependa de ellos (`InternalKeyAuthenticationHandler`,
`GetCurrentUserUseCase`, `ProvisionTenantUserUseCase`, etc.). Es una decisión
deliberada, no un olvido: tocarlos ahora habría roto el flujo de autenticación
humana vigente para no ganar nada todavía (la Fase 2 es la que corta esa
lectura hacia `tenant_members`). Lo que sí se agregó es la relación N:M en
paralelo (`TenantMember`), poblada por el backfill, para que cuando llegue la
Fase 2 los datos ya estén ahí.

### Onboarding de un cliente hoy (sin cambios por la Fase 1)

El orden real, sacado de los controllers (no cambia con este refactor, la capa
de Organization se agrega encima):

1. Operador registra el `Tenant` — `POST /api/v1/tenants` (RNC, razón social, plan).
2. Operador (o luego `admin_tenant` self-service) configura el `EmitterProfile`
   — `PUT /tenants/{id}/emitter-profile`.
3. Operador sube el certificado `.p12` — `POST /tenants/{id}/certificates`
   (archivo, contraseña, **ambiente**). Necesario para el bootstrap: sin esto
   no se puede acuñar la primera API key.
4. Operador registra el rango de secuencia e-NCF autorizado por la DGII —
   `POST /tenants/{id}/sequences` (tipo, serie, rango, **ambiente**).
5. Con cert + secuencia activos en un ambiente, ya se puede acuñar la primera
   API key — `POST /tenants/{id}/api-keys`.
6. Operador (o luego `admin_tenant`) da de alta a los usuarios del dashboard —
   `POST /tenants/{id}/users`.
7. (Nuevo, Fase 1) Operador crea la `Organization`, agrega ese usuario como
   miembro y asocia el tenant — pasos 2-4 de la sección de arriba. Todavía no
   cambia nada de lo que ve el cliente (ver la advertencia arriba).

Para pruebas internas sin certificado real existe `POST /api/v1/dev/sandbox`
(onboarding en un paso, cert autofirmado) — no es el flujo real de un cliente.

### Ambiente (Test/Cert/Production): no es un estado del Tenant

Aclaración importante para no diseñar mal la Fase 2/3: **no existe ni debería
existir un "mover el tenant a producción"**. El ambiente es una propiedad de
cada artefacto por separado, y todos conviven al mismo tiempo para el mismo
tenant:

- `Certificate.Environment` — un certificado vale para un solo ambiente.
- `NcfSequence.Environment` — un rango de secuencia también.
- `ApiKey.Environment` — la key queda atada al ambiente al acuñarla
  (`sk_nfe_test_...` vs `sk_nfe_prod_...`).
- `EmitterProfile.DefaultEnvironment` — es solo un *default* (para cuando la
  petición no especifica ambiente explícito), no un gate.

"Pasar a producción" = subir un certificado de producción + registrar la
secuencia de producción + acuñar una key de producción, sin tocar lo que ya
había en test. Un `admin_tenant` del cliente puede hacer todo esto él mismo
por la API una vez que el tenant ya tiene *algo* funcionando — el operador
solo es obligatorio en el bootstrap inicial (antes de la primera API key).

### Límite conocido de `AddOrganizationMember`

Agregar un miembro a una organización requiere que ya exista un
`PlatformUser` con ese correo (dado de alta hoy por `/tenants/{id}/users` u
`/operator-users`). Fase 1 no crea la identidad, solo la membresía — un flujo
de "invitar a alguien que todavía no tiene cuenta" es trabajo de Fase 2/3
(onboarding self-service), porque requiere decidir cómo se crea un
`PlatformUser` sin atarlo de entrada a un único tenant+rol (la limitación que
esta fase deliberadamente no tocó).

## Pendiente

### Fase 2 — Autorización humana (.NET)

- Cortar la lectura de tenant/rol de `PlatformUser.TenantId`/`Role` hacia
  `tenant_members`/`organization_members`.
- `InternalKeyAuthenticationHandler`: aceptar un header `X-Acting-Tenant-Id`,
  validar membresía (directa en `tenant_members`, o vía
  `organization_members` si un `owner`/`admin` de la organización debe ver
  todos sus tenants sin fila explícita), emitir el claim `tenant_id` con el
  rol efectivo en ese tenant.
- `GetCurrentUserUseCase`/`UserProfileDto`: devolver `organizations[].tenants[]`
  con el rol en cada uno, no un `tenantId`/`role` único.
- Endpoint para invitar a alguien sin cuenta previa (crea el `PlatformUser` +
  la membresía en un solo paso).
- Mover `TenantPlan` (o una métrica derivada) a `Organization` si se confirma
  que el plan se factura a nivel organización.
- **Pendiente de decidir** (a propósito, no resuelto todavía): qué operaciones
  de la jerarquía nueva quedan como self-service del cliente (`owner`/`admin`
  de organización, `admin_tenant` de tenant) vs. cuáles siguen requiriendo al
  operador — por ejemplo, crear una organización, crear un tenant nuevo dentro
  de una organización existente, o invitar a alguien sin cuenta previa. Hoy
  (Fase 1) *todo* el módulo `Organizations` es de operador porque no hay otra
  opción (no hay autorización self-service todavía); esa restricción no es la
  decisión final, es el punto de partida sobre el que la Fase 2 decide caso
  por caso.

### Fase 3 — Frontend (Next.js)

- Regenerar `schema.d.ts` contra la API ya cambiada en Fase 2.
- Rutas `[orgSlug]/[tenantSlug]/...` en vez del árbol plano actual bajo
  `(app)/*` (`Tenant` no tiene `Slug` todavía — hay que agregarlo, o resolver
  el segmento por otro identificador corto; es una decisión de esta fase).
- Extender `nav-tenant.tsx` (ya en curso en el working tree al momento de este
  análisis) de "menú de un tenant" a switcher real org→proyecto.
- Actualizar `identityHeaders()`, `use-current-user.ts`, `roles.ts`,
  `lib/navigation.ts` para el nuevo contrato.

### Fase 4 — Hardening

- Pruebas de integración: un usuario sin membresía en un tenant no puede
  fijar ese tenant como activo (403); RLS sigue aislando aunque se salte la
  capa de aplicación.
- Actualizar `docs/multi-tenancy.md`, `docs/human-auth.md`, `docs/api-auth.md`
  con la jerarquía nueva.

## Aplicar la migración

No corrida contra ninguna base de datos real todavía — solo generada y
probada contra el Postgres efímero de Testcontainers (`dotnet test`, que ya
pasó). Para aplicarla a tu base de desarrollo:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet dotnet-ef database update \
  --project src/Infrastructure --startup-project src/Service
```
