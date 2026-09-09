import { drizzle } from "drizzle-orm/node-postgres";
import { Pool } from "pg";

import { env } from "@/lib/env";

import * as schema from "./auth/schema";

/**
 * Pool de Postgres para Better Auth.
 *
 * Es un cliente **aparte** del de la API .NET: el dashboard no toca las tablas de
 * negocio (eso va por el proxy `app/api/backend`). Este pool existe solo para que
 * Better Auth guarde usuarios y sesiones en el schema `auth`. Mismo Neon, schema
 * distinto de `public` (que es territorio de EF Core).
 *
 * Driver `pg` (node-postgres), **no** `@neondatabase/serverless`: `pg` habla con
 * cualquier Postgres, así que mudarse de Neon es cambiar `DATABASE_URL` y nada más.
 */
const globalForDb = globalThis as unknown as { __authPool?: Pool };

const pool =
  globalForDb.__authPool ??
  new Pool({
    connectionString: env.DATABASE_URL,
    max: 5,
  });

// En dev, Next recarga módulos en cada cambio: sin esto se abriría un pool nuevo
// por recarga hasta agotar las conexiones de Neon.
if (process.env.NODE_ENV !== "production") globalForDb.__authPool = pool;

export const db = drizzle(pool, { schema });
