import "server-only";

import { headers } from "next/headers";

import { env } from "@/lib/env";

import { auth } from "./index";

/**
 * La sesión de Better Auth para la petición en curso, o `null`.
 *
 * Devuelve `null` también cuando la auth **no está configurada** (`DATABASE_URL` /
 * `BETTER_AUTH_SECRET` sin poner) o la base no responde — así el dashboard sigue
 * andando por el camino de desarrollo (`X-Tenant-Id`) mientras se termina de
 * configurar Better Auth.
 */
export async function getSession() {
  if (!env.DATABASE_URL || !env.BETTER_AUTH_SECRET) return null;

  try {
    return await auth.api.getSession({ headers: await headers() });
  } catch (error) {
    console.error("[auth] no se pudo leer la sesión:", error);
    return null;
  }
}
