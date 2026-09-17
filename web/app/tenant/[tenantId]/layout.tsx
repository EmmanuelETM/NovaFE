import { cookies } from "next/headers";

import {
  AppSidebar,
  AppSidebarProvider,
  AppTopbar,
  CommandPalette,
} from "@/components/shared/app-shell";
import { SidebarInset } from "@/components/ui/sidebar";
import { AccessScreen } from "@/features/auth/access-screen";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { ApiError } from "@/lib/api/problem";
import { apiFetch } from "@/lib/api/server";
import type { Scope } from "@/lib/navigation";

/**
 * El nombre de la cookie donde `SidebarProvider` guarda si está abierto o colapsado.
 *
 * Está duplicado del componente generado a propósito: `components/ui/` no se edita a mano
 * —lo reescribe la CLI de shadcn— así que no se le puede agregar un `export`. Si un
 * `shadcn add sidebar` cambiara este nombre, el único efecto sería que el sidebar aparece
 * abierto la primera vez.
 */
const SIDEBAR_COOKIE = "sidebar_state";

/**
 * El esqueleto depende de **quién** está mirando, así que no se puede prerrenderizar.
 *
 * Sin esto, Next resuelve `GET /users/me` una sola vez al construir y congela el
 * resultado: como en tiempo de compilación no hay API ni identidad, lo que quedaría
 * horneado en el HTML de todos es la pantalla de «sin acceso».
 */
export const dynamic = "force-dynamic";

/**
 * El esqueleto de las pantallas de un tenant (Fase 3).
 *
 * `params.tenantId` viene de la URL — es la **única** fuente del tenant
 * activo (nunca una cookie: dos pestañas en dos tenants no se pisan). Se lo
 * pasa a `apiFetch` como `tenantId`, que arma `X-Acting-Tenant-Id`; el
 * backend valida el acceso real (directo por `tenant_members`, o heredado si
 * sos owner/admin de la organización dueña) y devuelve el rol efectivo ahí —
 * si no tenés acceso a este tenant puntual, `GET /users/me` falla con 401 y
 * se muestra la misma pantalla de acceso denegado.
 *
 * Ver `app/(app)/layout.tsx` (el shell de `/nemus/**`, sin tenant) para la
 * versión sin esta pieza.
 */
export default async function TenantLayout({
  children,
  params,
}: LayoutProps<"/tenant/[tenantId]">) {
  const { tenantId } = await params;

  let user: CurrentUser;

  try {
    user = await apiFetch<CurrentUser>("/users/me", { tenantId });
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

  const store = await cookies();
  const scope: Scope = { kind: "tenant", tenantId };

  return (
    <AppSidebarProvider
      defaultOpen={store.get(SIDEBAR_COOKIE)?.value !== "false"}
    >
      <AppSidebar user={user} scope={scope} />

      <SidebarInset className="min-w-0 overflow-hidden">
        <AppTopbar user={user} scope={scope} />
        <CommandPalette user={user} scope={scope} />

        <div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
      </SidebarInset>
    </AppSidebarProvider>
  );
}
