# Autenticación de humanos (dashboard)

El dashboard (`web/`, M15) necesita login individual: sesión, revocación y
auditoría **por persona**. Las API keys (`docs/api-auth.md`) siguen intactas para
integración máquina-a-máquina; esto es el canal **humano**, y no reemplaza nada.

## Arquitectura: BFF + internal key

```
Browser ──cookie de sesión httpOnly (Better Auth, mismo origen)──► Next.js (web/)
                                                                     │
   El BFF lee la sesión server-side (auth.api.getSession) y reenvía:  │  X-Internal-Key: <secreto>
   El proxy app/api/backend/[...path] BORRA cualquier X-Internal-Key   │  X-Acting-User:  <id de Better Auth>
   / X-Acting-* que venga del browser y las pone él.                   │  X-Acting-Email: <correo>
                                                                       ▼
                                                                  API .NET
                                                     InternalKeyAuthenticationHandler:
                                                     1. valida X-Internal-Key (constant-time)
                                                     2. resuelve X-Acting-User / X-Acting-Email → PlatformUser
                                                     3. emite claims tenant_id + rol (idéntico a una API key)
                                                     → las políticas de M14 no distinguen el origen
```

**Por qué así y no un JWT que valide el .NET:** Better Auth usa sesiones de base
de datos por defecto, no JWT. El .NET queda **agnóstico del proveedor de
identidad** — no valida tokens de nadie, solo confía en su propio BFF (que sí
validó la sesión) a través del secreto compartido. Mudarse de Better Auth a otro
proveedor no toca el .NET.

### `web/` — Better Auth self-hosted

- **Self-hosted** (no el Managed Auth de Neon) por portabilidad: mudarse de Neon
  es cambiar `DATABASE_URL`. Driver de Postgres **por contrato** en `web/lib/db.ts`
  (`DATABASE_DRIVER` = `pg` | `neon`); correo por contrato en `web/lib/email.ts`.
- Tablas de Better Auth en el schema **`auth`** de la misma base (Drizzle +
  `drizzle-kit`), aparte de `public` (que es de EF Core). El .NET **no** lee esas
  tablas.
- Login: **OAuth** (Google, GitHub, Microsoft/Entra ID) **+ email/contraseña** con
  verificación de correo obligatoria.
- Guía del dashboard: `web/CLAUDE.md`.

## `PlatformUser` / `platform_users`

Modela la **identidad** de un humano: quién es y, para el operador del SaaS,
con qué rol. Operator-managed, **sin RLS** (igual que `api_keys` /
`emitter_profiles`: la resolución de identidad ocurre antes de que haya
tenant en la petición, y un operador no tiene tenant).

| Campo | |
| --- | --- |
| `email` | único, normalizado a minúsculas. **La clave de alta.** |
| `auth_user_id` | id opaco de Better Auth. `null` hasta el primer login. Único parcial. |
| `tenant_id` | **vestigial** para un usuario de contribuyente desde el refactor de organizaciones (ver más abajo); `null` = operador del SaaS. |
| `role` | `PlatformRole`: `admin_sistema` / `admin_tenant` / `emisor` / `consultor`. **Vestigial** para un usuario de contribuyente, mismo motivo. |
| `revoked_at` | `null` = vigente. **Global**: bloquea el login en cualquier tenant. Para revocar de un tenant puntual sin afectar el resto ver `tenant_members.revoked_at` más abajo. |

`PlatformRole` es un enum **separado** de `ApiKeyRole` (que tiene solo 3, sin
`admin_sistema` — ninguna API key de máquina puede representar al operador). Los 4
valores comparten los literales con las políticas.

### El tenant y el rol efectivos no salen de `PlatformUser` (jerarquía de organizaciones)

Desde el refactor `User -> Organization -> Tenant`
(`docs/multi-tenancy-hierarchy.md`), `PlatformUser.TenantId`/`Role` dejaron de
ser la fuente de verdad para un usuario de contribuyente — siguen en la
tabla (un operador sí los usa tal cual), pero para un humano de un tenant se
resuelven en cada login contra `tenant_members`/`organization_members`:

- Con `X-Acting-Tenant-Id` (el dashboard lo manda cuando el usuario ya eligió
  un tenant activo): acceso directo por `tenant_members`, o heredado como
  `admin_tenant` si el usuario es `owner`/`admin` de la organización dueña,
  sin fila explícita en `tenant_members`. Sin acceso a ese tenant puntual (o
  suspendido), la autenticación **falla** — no cae a un default silencioso.
- Sin el header: se toma el primer tenant accesible por antigüedad (directo,
  luego heredado). Si el usuario no tiene ningún tenant todavía —recién
  invitado a una organización—, el login **igual funciona** con
  `TenantId = null`.
- `GET /users/me` expone además `Organizations` — la lista completa de
  organizaciones del usuario, con sus tenants y el rol en cada una — que
  alimenta el switcher del dashboard. Ver el detalle completo en
  `docs/multi-tenancy-hierarchy.md`, no se repite acá.

Un `admin_tenant` puede serlo por dos caminos distintos y el login no los
distingue de cara al resto del sistema: una fila explícita en
`tenant_members`, o ser `owner`/`admin` de la organización dueña del tenant.

### Revocación: global vs. por tenant

Una persona puede tener acceso directo a varios tenants (una fila de
`tenant_members` por cada uno, con su propio rol). `DELETE
/tenants/{id}/users/{userId}` revoca **solo** la fila de ese tenant
(`tenant_members.revoked_at`) — el resto de sus accesos, y su capacidad de
loguearse, quedan intactos. `ResolveTenantAccessAsync`/
`ResolveDefaultTenantAccessAsync` ignoran una fila con `revoked_at` no nulo
(salvo que la persona además llegue heredado, como `owner`/`admin` de la
organización dueña — esa vía no depende de `tenant_members`).

`DELETE /operator-users/{userId}` es distinto: revoca
`PlatformUser.RevokedAt`, que sí es global — correcto ahí porque un operador
no tiene tenants que aislar.

### Provisioning por correo

El operador da de alta a alguien **por su correo** — no hace falta que la persona
haya entrado nunca. En el primer login, `InternalKeyAuthenticationHandler` resuelve
por correo, encuentra la fila pendiente y **enlaza** el `auth_user_id`
(best-effort; los siguientes lookups van por id). Si el correo cae en un usuario
ya enlazado a **otra** cuenta de Better Auth, se rechaza el acceso.

## Esquema `InternalKey`

- `Security:InternalApiKey` (env `Security__InternalApiKey`, Key Vault en
  producción). **Vacío = esquema deshabilitado** (ningún humano entra por esta vía).
- Header `X-Internal-Key` (comparación en tiempo constante) + `X-Acting-User` y/o
  `X-Acting-Email`, más `X-Acting-Tenant-Id` opcional (el tenant activo que
  eligió el usuario en el dashboard — ver la sección de arriba). **Sin la
  internal key correcta, ninguna de estas cabeceras `X-Acting-*` se
  respeta** — un cliente cualquiera no puede suplantar a nadie ni elegir el
  tenant de otro.
- Publica `NameIdentifier = user:{id}`, `ClaimTypes.Role`, y el claim `tenant_id`
  (si el usuario tiene tenant). Mismo shape que `ApiKeyAuthenticationHandler`.
- Entra en las políticas `TenantConfig` / `EcfIssue` / `EcfRead` (junto a `ApiKey`)
  y en `Operator` (junto a `AdminKey`). Nueva política `Authenticated` para
  `GET /users/me` — **sin `AdminKey`** (en Development ese handler autentica
  siempre sin clave, y se colaría como identidad primaria).

## Endpoints

Todos son recurso de **operador** (política `Operator`).

| Método | Ruta | |
| --- | --- | --- |
| `POST` | `/api/v1/tenants/{id}/users` | alta de un empleado del contribuyente (`{ email, role }`). Si el correo ya es `PlatformUser` (empleado de otro tenant, o de una organización) reusa esa cuenta y solo agrega la fila de `tenant_members` — un correo puede tener acceso a varios tenants. Crea esa fila siempre: sin eso el usuario queda con identidad pero sin acceso real a ese tenant. |
| `GET` | `/api/v1/tenants/{id}/users` | los usuarios de ese contribuyente (vía `tenant_members`, no `platform_users.tenant_id` — vestigial, ver arriba). |
| `DELETE` | `/api/v1/tenants/{id}/users/{userId}` | revoca de **este** tenant (idempotencia estricta: dos veces → 409). Ver "Revocación: global vs. por tenant". |
| `POST` | `/api/v1/operator-users` | alta de un operador (`{ email }`, rol fijo `admin_sistema`). |
| `GET` | `/api/v1/operator-users` | los operadores. |
| `DELETE` | `/api/v1/operator-users/{userId}` | revoca un operador. |

El **primer** operador se aprovisiona con el rompe-cristal `X-Admin-Key`; los
siguientes ya con la sesión de un operador.

Dar de alta o quitar a alguien de una **organización** (no de un tenant) no
vive acá — es self-service (`owner`/`admin` de esa organización, sin
operador), ver `docs/multi-tenancy-hierarchy.md`.

### `GET /api/v1/users/me`

Política `Authenticated`. Responde según quién sea:
- humano (`user:{id}`) → su `PlatformUser`, el tenant activo ya resuelto, y
  la lista completa de organizaciones a las que pertenece;
- cualquier otro esquema (API key, `X-Tenant-Id` de dev, operador) → un perfil
  derivado de los claims, para que el dashboard igual pinte algo.

```
{
  id, email, role,
  tenantId, tenantName,           // el tenant activo ya resuelto (o null)
  organizations: [
    {
      organizationId, organizationName, organizationSlug, plan, status, role,
      tenants: [{ tenantId, tenantName, role }]
    }
  ]
}
```

`organizations` es lo que consume el switcher del dashboard
(`docs/multi-tenancy-hierarchy.md`, Fase 3) — no hace falta una consulta
aparte para saber a qué tenants tiene acceso el usuario.

## Estado

Implementado y en uso: `PlatformUser` + `platform_users`,
`InternalKeyAuthenticationHandler` (con `X-Acting-Tenant-Id`), `GET /users/me`
(con `Organizations`), los endpoints de provisioning, y del lado del
dashboard `identityHeaders()` (`web/lib/api/server.ts`) ya manda la sesión
real en producción — el header `X-Tenant-Id` sin credencial solo existe en
Development. Login: OAuth (Google, GitHub, Microsoft) más email y
contraseña con verificación de correo y reset, todos con credenciales
configuradas y funcionando. Plan original de referencia (histórico):
`~/.claude/plans/linear-beaming-squirrel.md`.

## Fuera de alcance (v1)

- **Invitación automatizada a un tenant** — dar de alta a un empleado de un
  contribuyente no envía correo de "te dieron de alta"; el operador avisa
  por fuera. (A nivel organización sí hay invitación self-service —
  `POST /organizations/{id}/members`, ver `docs/multi-tenancy-hierarchy.md`
  — pero tampoco envía correo todavía.)
- **Que un `admin_tenant` invite empleados a su propio tenant** sin pasar
  por el operador (`POST /tenants/{id}/users` sigue siendo operador-only).
  Distinto de invitar miembros a una **organización**, que sí es
  self-service desde el refactor de organizaciones.
- **Revalidar la sesión de Better Auth en el .NET** (leer `auth.session`) —
  endurecimiento opcional si la internal key se filtra; hoy la internal key es el
  ancla de confianza.
