import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { nextCookies } from "better-auth/next-js";

import { db } from "@/lib/db";
import { env } from "@/lib/env";

import * as schema from "./schema";

/**
 * Better Auth, **self-hosted** en el dashboard. Ver el plan human-auth
 * (`~/.claude/plans/linear-beaming-squirrel.md`).
 *
 * - Adapter Drizzle, tablas en el schema `auth` de Neon (aparte de `public`,
 *   que es de EF Core en la API .NET). Driver `pg` → portable a cualquier Postgres.
 * - Solo **OAuth**: Google, GitHub, Microsoft (Entra ID). Sin passwords, sin
 *   magic links, sin proveedor de correo.
 * - `accountLinking` para que la misma persona por dos providers con el mismo
 *   email sea un solo `user`.
 *
 * Better Auth hace **solo autenticación** (identidad). La autorización —qué
 * tenant, qué rol— vive en la API .NET (`platform_users`), y el BFF la resuelve
 * reenviando `X-Internal-Key` + `X-Acting-User`/`X-Acting-Email`. Ver
 * `lib/api/server.ts`.
 */
export const auth = betterAuth({
  baseURL: env.BETTER_AUTH_URL,
  secret: env.BETTER_AUTH_SECRET,

  database: drizzleAdapter(db, {
    provider: "pg",
    schema,
  }),

  socialProviders: {
    google: {
      clientId: env.GOOGLE_CLIENT_ID ?? "",
      clientSecret: env.GOOGLE_CLIENT_SECRET ?? "",
    },
    github: {
      clientId: env.GITHUB_CLIENT_ID ?? "",
      clientSecret: env.GITHUB_CLIENT_SECRET ?? "",
    },
    microsoft: {
      clientId: env.MICROSOFT_CLIENT_ID ?? "",
      clientSecret: env.MICROSOFT_CLIENT_SECRET ?? "",
      tenantId: env.MICROSOFT_TENANT_ID,
    },
  },

  account: {
    accountLinking: {
      enabled: true,
      trustedProviders: ["google", "github", "microsoft"],
    },
  },

  // `nextCookies` va último en el array: hace que las cookies de sesión se
  // escriban bien desde server actions y route handlers de Next.
  plugins: [nextCookies()],
});
