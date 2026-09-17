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
 * Duplicado del componente generado a propósito — ver el mismo comentario en
 * `app/tenant/[tenantId]/layout.tsx`.
 */
const SIDEBAR_COOKIE = "sidebar_state";

/** Depende de quién está mirando (`GET /users/me`), así que no se prerrenderiza. */
export const dynamic = "force-dynamic";

/**
 * El esqueleto de las pantallas de una organización (Fase 4) — mismo shell
 * compartido que `/tenant/[tenantId]` y `/nemus/**` (`AppSidebarProvider` +
 * `AppSidebar` + `AppTopbar`), parametrizado con `scope: { kind: "organization" }`
 * en vez de un header a mano con pestañas planas.
 *
 * `params.orgSlug` se resuelve contra `UserProfileDto.organizations` (ya lo
 * trae `/users/me`, sin pegarle de nuevo al backend por id) — no existe un
 * endpoint self-service `GET /organizations/by-slug/{slug}`, y no hace
 * falta: el usuario logueado ya sabe a qué organizaciones pertenece.
 */
export default async function OrganizationLayout({
  children,
  params,
}: LayoutProps<"/org/[orgSlug]">) {
  const { orgSlug } = await params;

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

  const org = user.organizations.find((o) => o.organizationSlug === orgSlug);

  if (!org) {
    return (
      <AccessScreen
        status={403}
        message="No encontramos esa organización, o no tenés acceso."
      />
    );
  }

  const store = await cookies();
  const scope: Scope = { kind: "organization", orgSlug };

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
