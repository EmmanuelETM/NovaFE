import Link from "next/link";
import { Boxes } from "lucide-react";

import { SidebarMenuButton } from "@/components/ui/sidebar";
import type { CurrentUser } from "@/features/auth/use-current-user";
import type { Scope } from "@/lib/navigation";

import { ownerOrg } from "./org-switcher";

/** El nombre del sistema y el de la organización. */
const APP_NAME = "NovaFE";
const ORG_NAME = "Nemus Systems";

interface BrandProps {
  user: CurrentUser;
  scope: Scope;
}

/**
 * A dónde manda el logo — un nivel arriba de donde estás, no un reinicio
 * (patrón Supabase: el logo dentro de un proyecto lleva a la lista de
 * proyectos de esa organización, no a un "default" adivinado). Reusa
 * `ownerOrg`, la misma función que ya resuelve la organización dueña del
 * tenant activo para `OrgSwitcher`.
 */
function homeHref(user: CurrentUser, scope: Scope): string {
  if (scope.kind === "operator") return "/nemus/operacion";
  if (scope.kind === "organization") return "/organizaciones";

  const owner = ownerOrg(user, scope);
  return owner ? `/org/${owner.organizationSlug}` : "/organizaciones";
}

/**
 * La marca, arriba del sidebar.
 *
 * Lo que se lee primero es el nombre del sistema, no el de la organización: la
 * organización es el contexto y el sistema es el lugar. Al colapsar el sidebar queda solo
 * el icono, y el nombre lo dice el tooltip.
 *
 * Para poner un logo propio, sustituye el icono por un `next/image` de 40 px. Si es un
 * sello circular con texto en el aro, **no lo agrandes**: a ese tamaño el aro no se lee
 * pero la forma se reconoce, y agrandarlo convierte la cabecera del sidebar en una
 * portada.
 *
 * El enlace **no** va siempre a `/` — ver `homeHref`. Desde un tenant sube a
 * su organización dueña; desde una organización, al hub de todas; sin
 * organización visible (acceso directo), al hub, que también lista eso.
 */
export function Brand({ user, scope }: BrandProps) {
  return (
    <SidebarMenuButton
      size="lg"
      tooltip={`${APP_NAME} · ${ORG_NAME}`}
      className="gap-2.5"
      render={<Link href={homeHref(user, scope)} aria-label="Ir al inicio" />}
    >
      <div className="bg-sidebar-primary text-sidebar-primary-foreground flex size-10 shrink-0 items-center justify-center rounded-lg">
        <Boxes className="size-5" aria-hidden />
      </div>
      <div className="grid flex-1 text-left leading-tight">
        <span className="truncate text-base font-semibold">{APP_NAME}</span>
        <span className="text-muted-foreground truncate text-sm">
          {ORG_NAME}
        </span>
      </div>
    </SidebarMenuButton>
  );
}
