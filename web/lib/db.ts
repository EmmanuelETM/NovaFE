import { neonConfig, Pool as NeonPool } from "@neondatabase/serverless";
import { drizzle as drizzleNeon } from "drizzle-orm/neon-serverless";
import { drizzle as drizzlePg } from "drizzle-orm/node-postgres";
import { Pool as PgPool } from "pg";
import ws from "ws";

import { env } from "@/lib/env";

import * as schema from "./auth/schema";

/**
 * Cliente de Postgres para Better Auth.
 *
 * **Contrato**: el resto del código importa `db` (una instancia de Drizzle) y
 * nunca toca el driver. Cambiar de driver —o de proveedor de Postgres— es tocar
 * solo este archivo.
 *
 * `DATABASE_DRIVER`:
 *   - `pg` (default) — node-postgres, TCP. Portable a **cualquier** Postgres
 *     (Docker local, RDS, Azure, un branch de Neon…). Es el de desarrollo.
 *   - `neon` — `@neondatabase/serverless` sobre WebSocket. En Vercel evita el
 *     handshake TCP en cada invocación fría y no agota el límite de conexiones
 *     de Neon bajo concurrencia. Solo tiene sentido corriendo serverless
 *     contra Neon.
 *
 * Better Auth hace un lookup a la sesión en casi cada request autenticado, así
 * que en producción (Vercel + Neon) `neon` vale la pena; en local `pg` gana en
 * simplicidad y portabilidad. Mudarse de Neon: `DATABASE_DRIVER=pg`, y ya.
 *
 * Es un cliente **aparte** del de la API .NET: el dashboard no toca las tablas
 * de negocio (eso va por el proxy `app/api/backend`). Este pool existe solo
 * para el schema `auth`.
 */

// `@neondatabase/serverless` necesita una impl de WebSocket; Node 22 trae una
// nativa pero se fija explícita para que también ande en runtimes que no.
neonConfig.webSocketConstructor = ws;

function build() {
  const connectionString = env.DATABASE_URL;

  if (env.DATABASE_DRIVER === "neon") {
    return drizzleNeon(new NeonPool({ connectionString }), { schema });
  }

  return drizzlePg(new PgPool({ connectionString, max: 5 }), { schema });
}

// En dev, Next recarga módulos en cada cambio: sin cachear en globalThis se
// abriría un pool nuevo por recarga hasta agotar las conexiones. El pool se
// vuelve a construir si cambia el `DATABASE_URL` o el driver — así, si el server
// arrancó antes de que estuviera puesto, un `Reload env` lo repara sin reinicio.
const globalForDb = globalThis as unknown as {
  __authDb?: ReturnType<typeof build>;
  __authDbKey?: string;
};

const key = `${env.DATABASE_DRIVER}:${env.DATABASE_URL ?? ""}`;

export const db: ReturnType<typeof build> =
  globalForDb.__authDb && globalForDb.__authDbKey === key
    ? globalForDb.__authDb
    : build();

if (process.env.NODE_ENV !== "production") {
  globalForDb.__authDb = db;
  globalForDb.__authDbKey = key;
}
