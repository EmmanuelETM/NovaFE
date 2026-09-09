import "server-only";

import { env } from "@/lib/env";

import { ROLE } from "./roles";
import type { CurrentUser } from "./use-current-user";

/**
 * Perfil sintético para desarrollo, mientras NovaFE no expone `GET /users/me`.
 *
 * El endpoint de perfil humano llega con la autenticación de BetterAuth (plan
 * `~/.claude/plans/linear-beaming-squirrel.md`). Hasta entonces el esqueleto
 * necesita *algún* perfil para pintar la navegación, y en el camino `X-Tenant-Id`
 * de Development el rol es siempre `admin_tenant`.
 *
 * Es `null` cuando no hay `APP_DEV_TENANT_ID` configurado: ahí sí corresponde la
 * pantalla de acceso. Cuando exista `/users/me`, se borra este archivo y el
 * `catch` del layout vuelve a ser solo `AccessScreen`.
 */
export const devFallbackUser: CurrentUser | null = env.APP_DEV_TENANT_ID
  ? {
      id: env.APP_DEV_TENANT_ID,
      userName: "dev@novafe.local",
      displayName: "Desarrollo",
      roleId: ROLE.administrator,
      roleLabel: "Administrador (dev)",
    }
  : null;
