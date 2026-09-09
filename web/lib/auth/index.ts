import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { nextCookies } from "better-auth/next-js";

import { db } from "@/lib/db";
import { sendEmail } from "@/lib/email";
import { env } from "@/lib/env";

import * as schema from "./schema";

/**
 * Better Auth, **self-hosted** en el dashboard. Ver el plan human-auth
 * (`~/.claude/plans/linear-beaming-squirrel.md`).
 *
 * - Adapter Drizzle, tablas en el schema `auth` de Neon (aparte de `public`,
 *   que es de EF Core en la API .NET). Driver por contrato en `lib/db.ts`.
 * - Login: **OAuth** (Google, GitHub, Microsoft/Entra ID) **+ email/contraseña**
 *   con verificación de correo obligatoria.
 * - `accountLinking` para que la misma persona por dos vías con el mismo email
 *   sea un solo `user`.
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

  emailAndPassword: {
    enabled: true,
    // Sin verificar el correo no hay sesión: un `platform_users` se aprovisiona
    // por email, así que registrarse con el correo de otro no puede dar acceso.
    requireEmailVerification: true,
    minPasswordLength: 10,
    sendResetPassword: async ({ user, url }) => {
      await sendEmail({
        to: user.email,
        subject: "Restablecé tu contraseña — NovaFE",
        html: `<p>Pediste restablecer tu contraseña. Entrá a <a href="${url}">este enlace</a> para elegir una nueva.</p><p>Si no fuiste vos, ignorá este correo.</p>`,
      });
    },
  },

  emailVerification: {
    sendOnSignUp: true,
    autoSignInAfterVerification: true,
    sendVerificationEmail: async ({ user, url }) => {
      await sendEmail({
        to: user.email,
        subject: "Verificá tu correo — NovaFE",
        html: `<p>Confirmá tu correo entrando a <a href="${url}">este enlace</a>.</p>`,
      });
    },
  },

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
