import { cookies } from "next/headers";

import {
  AppSidebar,
  AppSidebarProvider,
  AppTopbar,
  CommandPalette,
  ImpersonationBanner,
} from "@/components/shared/app-shell";
import { SidebarInset } from "@/components/ui/sidebar";
import { AccessScreen } from "@/features/auth/access-screen";
import { InsufficientRoleScreen } from "@/features/auth/insufficient-role-screen";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { ApiError } from "@/lib/api/problem";
import { apiFetch } from "@/lib/api/server";
import {
  IMPERSONATION_COOKIE,
  parseImpersonationCookie,
} from "@/lib/impersonation";
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
 * El esqueleto de `/nemus/**` (Fase 3): recursos de **operador**, sin tenant.
 * Las pantallas de un tenant tienen su propio shell en
 * `app/tenant/[tenantId]/layout.tsx`, que además valida acceso al tenant de
 * la URL — acá el equivalente es el chequeo de rol de abajo: `GET /users/me`
 * puede devolver perfectamente a un `admin_tenant`/`emisor` (por ejemplo, si
 * escribe la URL a mano), y sin ese chequeo el shell entero de Nemus Admin
 * se pintaba igual para cualquiera — el filtro de `lib/navigation.ts` solo
 * esconde el enlace del sidebar, no protege la ruta.
 *
 * El perfil se resuelve **en el servidor**, y de ahí sale la navegación. Hacerlo aquí y no
 * con un hook tiene dos consecuencias buenas: el sidebar sale correcto en el primer
 * pintado, sin enlaces que aparecen y desaparecen, y quien no tiene acceso lo sabe antes de
 * ver una pantalla vacía.
 */
export default async function AppLayout({ children }: LayoutProps<"/">) {
  let user: CurrentUser;

  try {
    user = await apiFetch<CurrentUser>("/users/me");
  } catch (error) {
    // Sin sesión (o sin `APP_DEV_TENANT_ID` en dev) la API responde 401 y acá se
    // muestra la pantalla de acceso. El `proxy.ts` redirige a `/login` antes
    // de llegar acá en el caso normal; esto cubre la sesión que expira entre el
    // middleware y el render, o un usuario autenticado sin `platform_users`.
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

  if (user.role !== "admin_sistema") {
    return <InsufficientRoleScreen role={user.role} />;
  }

  const store = await cookies();
  const scope: Scope = { kind: "operator" };
  const impersonation = parseImpersonationCookie(
    store.get(IMPERSONATION_COOKIE)?.value,
  );

  return (
    /* El alto fijo y la excepción del punto de venta viven en `AppSidebarProvider`: las
       dos necesitan la ruta, y un layout de servidor no la sabe. */
    <AppSidebarProvider
      defaultOpen={store.get(SIDEBAR_COOKIE)?.value !== "false"}
    >
      <AppSidebar user={user} scope={scope} />

      {/* El scroll vive aquí y no en el documento: así la barra superior y el sidebar se
          quedan quietos sin `sticky`, y el redondeado del panel recorta lo que pasa por
          debajo en vez de dejarlo asomar por la esquina. */}
      <SidebarInset className="min-w-0 overflow-hidden">
        {impersonation && <ImpersonationBanner email={impersonation.email} />}
        <AppTopbar user={user} scope={scope} />
        <CommandPalette user={user} scope={scope} />

        <div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
      </SidebarInset>
    </AppSidebarProvider>
  );
}
