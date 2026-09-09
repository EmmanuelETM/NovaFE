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

Modela la **autorización** de un humano: a qué contribuyente pertenece y con qué
rol. Operator-managed, **sin RLS** (igual que `api_keys` / `emitter_profiles`: la
resolución de identidad ocurre antes de que haya tenant en la petición, y un
operador no tiene tenant).

| Campo | |
| --- | --- |
| `email` | único, normalizado a minúsculas. **La clave de alta.** |
| `auth_user_id` | id opaco de Better Auth. `null` hasta el primer login. Único parcial. |
| `tenant_id` | el contribuyente; `null` = operador del SaaS. |
| `role` | `PlatformRole`: `admin_sistema` / `admin_tenant` / `emisor` / `consultor`. |
| `revoked_at` | `null` = vigente. |

`PlatformRole` es un enum **separado** de `ApiKeyRole` (que tiene solo 3, sin
`admin_sistema` — ninguna API key de máquina puede representar al operador). Los 4
valores comparten los literales con las políticas.

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
  `X-Acting-Email`. **Sin la internal key correcta, las cabeceras `X-Acting-*` se
  ignoran** — un cliente cualquiera no puede suplantar a nadie.
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
| `POST` | `/api/v1/tenants/{id}/users` | alta de un empleado del contribuyente (`{ email, role }`). |
| `GET` | `/api/v1/tenants/{id}/users` | los usuarios de ese contribuyente. |
| `DELETE` | `/api/v1/tenants/{id}/users/{userId}` | revoca (idempotencia estricta: dos veces → 409). |
| `POST` | `/api/v1/operator-users` | alta de un operador (`{ email }`, rol fijo `admin_sistema`). |
| `GET` | `/api/v1/operator-users` | los operadores. |
| `DELETE` | `/api/v1/operator-users/{userId}` | revoca un operador. |

El **primer** operador se aprovisiona con el rompe-cristal `X-Admin-Key`; los
siguientes ya con la sesión de un operador.

### `GET /api/v1/users/me`

Política `Authenticated`. Responde según quién sea:
- humano (`user:{id}`) → su `PlatformUser` + la razón social del contribuyente;
- cualquier otro esquema (API key, `X-Tenant-Id` de dev, operador) → un perfil
  derivado de los claims, para que el dashboard igual pinte algo.

`{ id, email?, role, tenantId?, tenantName? }`.

## Estado

Implementado del lado .NET: `PlatformUser` + `platform_users`,
`InternalKeyAuthenticationHandler`, `GET /users/me`, y los endpoints de
provisioning. El dashboard todavía manda `X-Tenant-Id` (dev): reescribir
`identityHeaders()` en `web/lib/api/server.ts` y las pantallas de login son los
slices siguientes. Ver `~/.claude/plans/linear-beaming-squirrel.md`.

## Fuera de alcance (v1)

- **Un tenant por usuario.** Sin tabla de unión N a N (una firma contable con
  varios clientes es futuro).
- **Invitación automatizada** — v1 no envía correo de "te dieron de alta"; el
  operador avisa por fuera.
- **Que un `admin_tenant` invite a sus compañeros** sin pasar por el operador.
- **Revalidar la sesión de Better Auth en el .NET** (leer `auth.session`) —
  endurecimiento opcional si la internal key se filtra; hoy la internal key es el
  ancla de confianza.
