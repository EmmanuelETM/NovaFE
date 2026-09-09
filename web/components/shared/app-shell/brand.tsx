import Link from "next/link";
import { Boxes } from "lucide-react";

import { SidebarMenuButton } from "@/components/ui/sidebar";
import { HOME_HREF } from "@/lib/navigation";

/** El nombre del sistema y el de la organización. */
const APP_NAME = "NovaFE";
const ORG_NAME = "Nemus Systems";

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
 */
export function Brand() {
  return (
    <SidebarMenuButton
      size="lg"
      tooltip={`${APP_NAME} · ${ORG_NAME}`}
      className="gap-2.5"
      render={<Link href={HOME_HREF} aria-label="Ir al inicio" />}
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
