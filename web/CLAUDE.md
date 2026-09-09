# CLAUDE.md

Guía para Claude Code (claude.ai/code) al trabajar en este repositorio.

---

# NovaFE — Dashboard

Panel de administración de NovaFE (operadores y empleados del contribuyente: ven su
facturación y su configuración). Es un proyecto **aparte** del backend .NET, vive en
`web/` dentro del monorepo, y se despliega en **Vercel**. El backend (ASP.NET Core) vive
en `../src/`; su guía es `../CLAUDE.md`.

Base: Next.js 16 (App Router) + React 19 + Tailwind 4 + shadcn sobre Base UI, con TanStack
Query y Table, react-hook-form + Zod, nuqs y Zustand ya cableados.

## Conexión con la API NovaFE

- **`APP_API_URL`** en `.env.local` — `http://localhost:5071` con
  `dotnet run --project ../src/Service`, o `http://localhost:8080` con Visual
  Studio F5 / `docker compose up`. Cada camino usa una BD distinta (dotnet run =
  user-secrets/Neon; docker compose = Postgres del contenedor), así que el
  `APP_DEV_TENANT_ID` tiene que existir en la que estés usando (créalo con
  `POST /api/v1/dev/sandbox` contra esa instancia). NovaFE versiona por ruta:
  `APP_API_VERSION=v1`.
- **Identidad** (`identityHeaders()` en `lib/api/server.ts`, es `async`):
  1. hay sesión de Better Auth → `X-Internal-Key` + `X-Acting-User`/`X-Acting-Email`;
  2. si no y `APP_DEV_TENANT_ID` está seteado → `X-Tenant-Id` (esquema `DevTenantHeader`
     de NovaFE, solo en Development, rol fijo `admin_tenant`);
  3. si no → nada, y la API responde 401.
     El proxy `app/api/backend/[...path]` **borra** `x-internal-key`/`x-acting-*`/`x-tenant-id`
     (y `x-api-key`/`x-admin-key`) del request entrante — el browser no puede inyectarlas.
- **`GET /users/me`** ya existe en la API (`UserProfileDto`). El layout lo pide server-side
  y de ahí sale la navegación por rol. Sin sesión → `AccessScreen` con enlace a `/login`.

## Auth humana — Better Auth (CABLEADO — slices G/H/I)

Plan completo: `~/.claude/plans/linear-beaming-squirrel.md` (sección "Auth humano").
Referencia del lado .NET: `../docs/human-auth.md`.

- **`proxy.ts`** (antes `middleware.ts` — Next 16) redirige a `/login` si no hay cookie de
  sesión (lectura sin I/O; no valida). Salta el gate si `APP_DEV_TENANT_ID` está seteado.
- **`app/(auth)/login`** — un botón por cada provider OAuth con credenciales en el entorno
  (`enabledSocialProviders()` en `lib/auth/providers.ts`). Se van habilitando de a uno:
  hoy **solo GitHub**. `app/(auth)/auth-error` recibe los fallos de OAuth.
- **Logout** en `nav-user.tsx` (`authClient.signOut()` → `/login`).
- **Roles**: `features/auth/roles.ts` — los 4 de la API (`consultor` < `emisor` <
  `admin_tenant` < `admin_sistema`) con rango numérico; `roleRank()` / `roleLabel()`.
  `lib/navigation.ts` filtra por `minRole`. `CurrentUser` = `UserProfileDto` de la API.
- **Self-hosted** (no el Managed de Neon) por portabilidad.
- **Driver por contrato** (`lib/db.ts`): `DATABASE_DRIVER=pg` (default, node-postgres,
  portable a cualquier Postgres) o `neon` (`@neondatabase/serverless` sobre WebSocket,
  para Vercel + Neon). El resto del código importa `db` y no sabe cuál es. Mudarse de
  Neon = `DATABASE_DRIVER=pg`.
- **Login**: OAuth (GitHub habilitado; Google + Microsoft cuando se pongan sus
  credenciales) **+ email/contraseña** con verificación de correo obligatoria (la UI de
  email/contraseña todavía no está — solo los botones OAuth). Cada provider OAuth se
  registra en `lib/auth/index.ts` solo si sus `*_CLIENT_ID`/`*_CLIENT_SECRET` están.
- **Correo por contrato** (`lib/email.ts`): Resend, o log a consola en dev sin
  `RESEND_API_KEY`. Swappable a Azure Communication Services.
- Tablas en el schema **`auth`** de Neon (Drizzle + `drizzle-kit`), aparte de `public`
  (que es de EF Core en la API). El .NET **no** lee esas tablas.
- Archivos: `lib/db.ts` (contrato de driver + Drizzle), `lib/email.ts` (contrato de
  correo), `lib/auth/index.ts` (config), `lib/auth/schema.ts` (tablas, envueltas a mano
  en `pgSchema("auth")` — ver el comentario del archivo antes de regenerar),
  `lib/auth/{client,providers,social}.ts`, `app/api/auth/[...all]/route.ts`.
- Autorización (tenant + rol) **no** vive acá — vive en `platform_users` en la API .NET.
  Better Auth solo hace autenticación.
- Scripts: `bun run auth:generate` (CLI → `schema.generated.ts` para diffear),
  `bun run db:generate` / `db:migrate` (drizzle-kit, schema `auth`; el `.sql` va
  versionado en `drizzle/`). Recomendado: apuntar `DATABASE_URL` a un **branch `dev` de
  Neon** para no tocar `production`.
- Env: `DATABASE_URL` (requerida para auth), `DATABASE_DRIVER`, `BETTER_AUTH_SECRET`,
  `BETTER_AUTH_URL`, `INTERNAL_API_KEY` (= `Security:InternalApiKey` de la API),
  `RESEND_API_KEY`, `EMAIL_FROM`, `GITHUB_*` (y `GOOGLE_*` / `MICROSOFT_*` cuando toque).
- **Neon MCP**: agregado a la config de Claude Code (`claude mcp add neon …`). Autenticar
  con `/mcp`. Útil para el schema `auth`, branches de Neon, y el rol `novafe_app` pendiente.
- **`bun run api:types`** apunta a `http://localhost:5071/openapi/v1.json` (solo mapeado en
  Development). Regenera `lib/api/schema.d.ts` con la API corriendo.

## No hay hook de pre-commit

A diferencia del scaffold original, aquí **no** hay husky/lint-staged (viven mal en un
subdirectorio de un repo git existente). La verificación corre en CI:
`.github/workflows/web-ci.yml` (lint + typecheck + build + format:check). Antes de
commitear, corre `bun run format` y `bun run lint` a mano.

## Antes de escribir una línea: tres cosas que rompen

Este proyecto usa versiones más nuevas que la mayoría de los ejemplos que hay en
circulación. Escribir de memoria aquí produce código que **no compila**.

### 1. Next.js 16

Lee la guía correspondiente en `node_modules/next/dist/docs/` antes de tocar rutas,
`params`, caché o server actions.

Un ejemplo concreto que ya está en el código: los `params` de un route handler son una
**promesa** y hay que esperarlos (`const { path } = await context.params`).

### 2. TanStack Table v9, que no se parece a la v8

| Lo que sale de memoria (v8) | Lo correcto (v9)                                         |
| --------------------------- | -------------------------------------------------------- |
| `useReactTable`             | `useTable`                                               |
| features implícitas         | `tableFeatures({ rowPaginationFeature, ... })` explícito |
| `createColumnHelper<T>()`   | `createColumnHelper<typeof features, T>()`               |
| `flexRender(...)`           | `<table.FlexRender header={header} />`                   |
| `row.getVisibleCells()`     | `row.getAllCells()`                                      |
| estado con `useState`       | átomos: `useCreateAtom` de `@tanstack/react-store`       |

**El paquete trae sus propias skills.** Léelas en vez de adivinar:

- `node_modules/@tanstack/react-table/skills/` — `getting-started`, `table-state`,
  `with-tanstack-query`, `create-table-hook`, `migrate-v8-to-v9`
- `node_modules/@tanstack/table-core/skills/` — 23 más, una por _feature_, incluidas
  `pagination`, `column-filtering`, `sorting`, `client-vs-server`, `typescript`

### 3. shadcn sobre Base UI, no sobre Radix

Los componentes de `components/ui/` están construidos sobre **`@base-ui/react`**. Las APIs
de Radix (`Root`/`Trigger`/`Portal` con esos nombres y props) no aplican. Estilo
`base-rhea`, color base neutral.

`components/ui/` **se genera con la CLI de shadcn** — no se edita a mano. Para agregar un
componente: `bunx shadcn@latest add <nombre>`. Si hace falta un comportamiento propio, se
envuelve en `components/shared/`, no se modifica el generado.

Un detalle de Base UI que muerde: **todo `Select` lleva el mapa `items` en la raíz**. Sin
él pinta el valor en bruto en el disparador en vez de la etiqueta. Para eso está
`selectItems()` en `lib/select-items.ts`.

---

## Arquitectura

```
Navegador  ──mismo origen──►  Next.js  ──identidad──►  API
```

El navegador **nunca llama a la API directamente**. Pasa por el proxy de
`app/api/backend/[...path]`. De ahí salen tres cosas: no hace falta CORS, la API puede no
ser pública, y el token nunca llega al navegador.

Hay **dos clientes** y no se mezclan:

- `lib/api/server.ts` — componentes y layouts de servidor. Es la única costura de
  identidad del proyecto: `identityHeaders()` decide cómo se autentica, y solo ahí.
- `lib/api/client.ts` — componentes de cliente, a través del proxy. Sin cabeceras de
  identidad: eso lo pone el servidor.

Los errores de los dos salen como `ApiError` (`lib/api/problem.ts`), con el ProblemDetails
ya interpretado, así que un solo manejador cubre validación y negocio.

## La identidad

Ver **Conexión con la API NovaFE** arriba. En corto: en desarrollo va `X-Tenant-Id` desde
`APP_DEV_TENANT_ID`; en producción llega BetterAuth. **Cambiar `identityHeaders()` en
`lib/api/server.ts` es lo único que hace falta** — ninguna pantalla se entera.

El esqueleto pide `GET /users/me` en `app/(app)/layout.tsx` y de ahí sale el rol que filtra
la navegación. Ese endpoint aún no existe en NovaFE; el fallback de desarrollo está en
`features/auth/dev-user.ts`.

## Convenciones

### Idioma

- **Identificadores en inglés**, siguiendo los tipos de la API: si el DTO es `ProductDto`,
  el tipo aquí es `Product` y no `Producto`.
- **Comentarios en español.**
- **Todo lo que el usuario lee, en español.** Y los mensajes de error **no se reescriben**:
  la API los devuelve listos para mostrar. Cambiarlos hace que la pantalla y el log no
  coincidan, y eso vuelve imposible depurar por teléfono.
- **Rutas en español**, porque son visibles.

### Estructura

```
app/
  (app)/                       las pantallas, con sidebar y barra superior
  api/backend/[...path]/       proxy hacia la API
components/
  ui/                          shadcn generado — no editar
  shared/app-shell/            sidebar, barra superior, encabezado de pantalla
  shared/data-table/           la tabla compartida
  shared/                      lo que se usa en más de una feature
features/<modulo>/             componentes, hooks y tipos de ese módulo
lib/
  navigation.ts                la navegación, definida una sola vez
  api/                         schema generado, clientes, errores, claves de query
  format/                      dinero, fechas, cantidades, autores
hooks/
```

**Feature-first**: lo que pertenece a un módulo vive junto. Agrupar por tipo de archivo
—todos los hooks en una carpeta— obliga a abrir cuatro carpetas para entender una pantalla.

### TypeScript estricto

**Nada de `any`.** Es un error de ESLint, no un aviso. Y no es purismo: los tipos de la API
vienen generados del OpenAPI, así que un `any` no es un atajo — es tirar a la basura la
única garantía de que el nombre del campo existe. Cuando de verdad no se sabe el tipo,
`unknown` obliga a estrecharlo antes de usarlo.

Tampoco: `@ts-ignore`, `@ts-expect-error` sin explicación, ni casts en cadena
(`as unknown as T`) para callar al compilador.

Además de `strict`, el `tsconfig.json` activa:

| Bandera                      | Qué atrapa                                                                                      |
| ---------------------------- | ----------------------------------------------------------------------------------------------- |
| `noUncheckedIndexedAccess`   | `arr[0]` es `T \| undefined`. **La que más errores reales encuentra**, y `strict` no la incluye |
| `noImplicitReturns`          | una rama de función que se olvidó de devolver                                                   |
| `noFallthroughCasesInSwitch` | un `case` sin `break`                                                                           |
| `noImplicitOverride`         | sobrescribir un método sin decirlo                                                              |

El `!` es un **aviso**, no un error: a veces es correcto, después de comprobar algo que el
compilador no puede seguir. Pero cada uno merece una mirada. Las variables sin usar se
prefijan con `_` cuando se ignoran a propósito.

### Tipos de la API

`lib/api/schema.d.ts` está **generado** y no se edita. En el scaffold viene vacío a
propósito. Ajusta la URL del script en `package.json` y, con la API corriendo:

```bash
bun run api:types
```

Nunca escribas a mano el tipo de una respuesta. Si un campo no existe en el schema, o está
mal el nombre, es un error de compilación — que es el punto.

### Datos del servidor

**TanStack Query para todo lo que venga de la API.** Sin `useEffect` + `useState` para
cargar datos, y sin duplicar el resultado de una query en estado local.

**Todas las claves salen de `lib/api/query-keys.ts`.** Sin excepción. Es lo que separa
invalidar la caché con confianza de ir adivinando qué cadena se usó en el otro archivo.

Las políticas ya están en `app/providers.tsx`: un minuto de `staleTime`, sin refetch al
recuperar el foco, sin reintentar 4xx ni errores del proxy, y **mutaciones sin reintento**
— una operación que se reintenta sola se puede ejecutar dos veces.

### Tablas

**Nunca `<table>`, `<tr>`, `<th>` sueltos.** Toda tabla usa el `DataTable` de
`components/shared/data-table/`, que combina TanStack Table v9 con los componentes de
shadcn. Está pensado para paginación, ordenamiento y filtros **del lado del servidor**. Si
necesitas algo que no soporta, se extiende el `DataTable` — no se escribe una tabla suelta.

### Navegación

**`lib/navigation.ts` es la única definición.** De ahí salen los enlaces del sidebar, la
miga de pan de la barra superior y el `h1` de cada pantalla. Agregar una pantalla es:
agregar un item ahí con `ready: true`, crear `app/(app)/<ruta>/page.tsx` y usar
`<PageHeader href="…" />`. Un item con `ready: false` se pinta inerte con la etiqueta
«pronto».

### Estado de cliente

- **URL** para lo que debe ser compartible o sobrevivir a un refresco: página, filtros,
  ordenamiento, pestaña activa. Con `nuqs`.
- **Zustand** solo para estado global de verdad y justificado.
- **`useState`** para lo local. Un diálogo abierto no es estado global.

Si dudas entre Zustand y la URL, es la URL.

### Formularios

`react-hook-form` con `zodResolver`. El esquema de Zod valida la **forma**; las reglas de
negocio las valida la API y vuelven como errores por campo — `applyFieldErrors()` en
`lib/api/form-errors.ts` los pega al formulario, convirtiendo el PascalCase de la API a
camelCase de paso.

### Dinero y fechas

- **La UI no calcula dinero.** Los importes los calcula la API.
- Formato **solo** desde `lib/format`. Ajusta ahí la moneda, el locale y la zona horaria:
  el scaffold viene con `es-DO`, `DOP` y `America/Santo_Domingo`, y la zona va
  **explícita** para que agrupar por día no dependa de la del navegador.

---

## Comandos

```bash
bun install
bun dev                 # desarrollo
bun run typecheck       # tsc --noEmit — córrelo antes de decir que algo está listo
bun run lint
bun run format          # prettier --write .
bun run format:check    # sin escribir, para CI
bun run api:types       # regenera los tipos desde el OpenAPI (API corriendo)
bun run build
```

### Formato

Prettier manda: 2 espacios y 80 columnas, **LF** (hay un `.editorconfig` con `root = true`
para que el de la raíz —4 espacios, CRLF, del backend .NET— no se filtre). **No discutas el
formato, córrelo.** Los 2 espacios son lo que emite el generador de shadcn.
`prettier-plugin-tailwindcss` ordena las clases solo.

No hay hook de pre-commit (ver arriba). Corre `bun run format` y `bun run lint` antes de
commitear; CI los verifica.

### Node

Next 16 necesita **Node 20 o superior**. El repo declara `22.20.0` en `.node-version`.

Si `eslint` falla con `Object.hasOwn is not a function`, o `next build` no arranca, es que
el shell quedó en una versión vieja. Compruébalo con `node --version` antes de buscar el
problema en el código — el síntoma no se parece en nada a la causa.

### El proxy responde 502

`Proxy.ApiUnreachable` o `Proxy.ApiTimeout` significan que Next no pudo hablar con la API.
Mira `APP_API_URL` en `.env.local` y que la API esté arriba. El detalle técnico va al log
del servidor de Next, no al navegador — eso es a propósito.

<!-- BEGIN:nextjs-agent-rules -->

# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->
