import { redirect } from "next/navigation";

import { AccessScreen } from "@/features/auth/access-screen";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { ApiError } from "@/lib/api/problem";
import { apiFetch } from "@/lib/api/server";

export const dynamic = "force-dynamic";

/**
 * El punto de aterrizaje (Fase 3). Vive **fuera** de `(app)` a propósito: no
 * necesita el shell de sidebar (que además exige un `tenantId` de ruta) para
 * decidir a dónde mandar a alguien. Pide `/users/me` sin `tenantId` — el
 * backend devuelve el tenant activo por defecto si hay alguno — y redirige:
 *
 * 1. Operador (`admin_sistema`) → `/nemus/operacion`.
 * 2. Tenant activo resuelto → `/tenant/{tenantId}` (o el último elegido, si
 *    el switcher dejó la cookie `last_tenant_id` y sigue siendo válido).
 * 3. Sin tenant pero con alguna organización → `/org/{slug}`.
 * 4. Nada de lo anterior (usuario recién invitado, sin organización todavía)
 *    → pantalla neutra.
 */
export default async function RootPage() {
  let user: CurrentUser;

  try {
    user = await apiFetch<CurrentUser>("/users/me");
  } catch (error) {
    const esDeLaApi = error instanceof ApiError;

    return (
      <AccessScreen
        status={esDeLaApi ? error.status : 0}
        message={
          esDeLaApi ? error.message : "No se pudo conectar con el servidor."
        }
      />
    );
  }

  if (user.role === "admin_sistema") redirect("/nemus/operacion");

  if (user.tenantId) redirect(`/tenant/${user.tenantId}`);

  const firstOrg = user.organizations.at(0);
  if (firstOrg) redirect(`/org/${firstOrg.organizationSlug}`);

  return (
    <AccessScreen
      status={0}
      message="Todavía no tenés acceso a ninguna organización. Contactá a quien te invitó."
    />
  );
}
