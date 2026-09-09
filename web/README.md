# novafe-web

Dashboard de NovaFE: **Next.js 16** (App Router) + **React 19** + **Tailwind 4** + **shadcn
sobre Base UI**. Se despliega en Vercel; habla con la API .NET (`../src/`) a través de un
proxy de mismo origen. Ver [`CLAUDE.md`](./CLAUDE.md) para la conexión con la API y la
identidad.

## Qué trae

| Área               | Elección                                                          |
| ------------------ | ----------------------------------------------------------------- |
| UI                 | shadcn (estilo `base-rhea`, base neutral) sobre `@base-ui/react`  |
| Estilos            | Tailwind 4, tema claro/oscuro con `next-themes`                   |
| Datos del servidor | TanStack Query + devtools, claves centralizadas                   |
| Tablas             | TanStack Table **v9** envuelto en un `DataTable` compartido       |
| Formularios        | react-hook-form + Zod, con errores de la API pegados a los campos |
| Estado en la URL   | nuqs                                                              |
| Estado global      | Zustand                                                           |
| Gráficos           | Recharts (`components/ui/chart`)                                  |
| Notificaciones     | sonner                                                            |
| Entorno            | `@t3-oss/env-nextjs`, validado al arrancar                        |
| Tipos de la API    | `openapi-typescript` (`bun run api:types`)                        |
| Calidad            | TypeScript estricto, ESLint 9, Prettier, husky + lint-staged      |

Y el esqueleto: sidebar colapsable, barra superior con miga de pan, menú de usuario con
selector de tema, encabezado de pantalla, tiles de estadística, y un **proxy de mismo
origen** hacia tu API con manejo de ProblemDetails (RFC 9457).

## Arrancar

```bash
bun install
cp .env.example .env.local   # y pon la URL de tu API en APP_API_URL
bun dev
```

Abre <http://localhost:3000>.

## Arrancar en local

1. Levanta la API: `dotnet run --project ../src/Service` (escucha en `:5071`).
2. `cp .env.example .env.local` y pon `APP_DEV_TENANT_ID` = id de un contribuyente
   registrado (`POST /api/v1/dev/sandbox` da uno de un tiro).
3. `bun run api:types` con la API corriendo → genera `lib/api/schema.d.ts`.
4. `bun dev`.

## Comandos

```bash
bun dev                 # desarrollo
bun run typecheck       # tsc --noEmit
bun run lint
bun run format
bun run api:types       # regenera los tipos desde el OpenAPI (API corriendo)
bun run build
```

Las convenciones del proyecto y las tres cosas que rompen si escribes de memoria están en
[`CLAUDE.md`](./CLAUDE.md).
