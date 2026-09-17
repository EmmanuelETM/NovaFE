"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import {
  SidebarGroup,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from "@/components/ui/sidebar";
import {
  NAVIGATION,
  findNavItem,
  tenantHref,
  visibleNavItems,
  type NavItem,
} from "@/lib/navigation";

interface NavMainProps {
  /** Nivel del usuario, resuelto en el servidor. */
  role: number;
  /**
   * El tenant activo (Fase 3), o `undefined` en `/nemus/**` (sin tenant). No
   * es solo para armar los `href`: decide **qué ámbito** de `NAVIGATION` se
   * pinta — con tenant, las secciones de tenant; sin él, solo "Nemus Admin".
   */
  tenantId?: string;
}

/**
 * Los destinos, agrupados.
 *
 * El rol llega como prop y no de un hook: se resuelve en el servidor, así que la
 * navegación sale correcta en el primer pintado. Con una consulta del cliente aparecerían
 * primero los enlaces de todos y luego se irían los que no corresponden, y un menú que se
 * reacomoda medio segundo después se siente como un fallo.
 */
export function NavMain({ role, tenantId }: NavMainProps) {
  const pathname = usePathname();
  const { setOpenMobile } = useSidebar();

  const actual = findNavItem(pathname);

  return (
    <>
      {NAVIGATION.filter(
        (section) =>
          !section.hideFromSidebar &&
          (tenantId
            ? section.scope !== "operator"
            : section.scope === "operator"),
      ).map((section) => {
        const visibles = visibleNavItems(section.items, role);

        // Un grupo cuyo único contenido estaba fuera del alcance del rol no debe dejar
        // el título flotando sobre nada.
        if (visibles.length === 0) return null;

        return (
          <SidebarGroup key={section.label}>
            <SidebarGroupLabel>{section.label}</SidebarGroupLabel>
            <SidebarMenu>
              {visibles.map((item) => (
                <SidebarMenuItem key={item.href}>
                  {item.ready ? (
                    <SidebarMenuButton
                      isActive={item.href === actual?.href}
                      tooltip={item.label}
                      render={
                        <Link
                          href={
                            tenantId
                              ? tenantHref(tenantId, item.href)
                              : item.href
                          }
                          onClick={() => setOpenMobile(false)}
                        />
                      }
                    >
                      <item.icon aria-hidden />
                      <span>{item.label}</span>
                    </SidebarMenuButton>
                  ) : (
                    <PendingItem item={item} />
                  )}
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroup>
        );
      })}
    </>
  );
}

/**
 * Un módulo que la API ya sirve pero que todavía no tiene pantalla.
 *
 * Se pinta inerte en vez de esconderse: así el sidebar muestra la forma real de la
 * aplicación sin ofrecer un camino que termina en un 404.
 *
 * No usa `disabled` a propósito. Con `disabled` el navegador deja de mandar eventos de
 * puntero, y con el sidebar colapsado eso deja un icono sin etiqueta y sin tooltip — que
 * es justo cuando la etiqueta hace falta.
 */
function PendingItem({ item }: { item: NavItem }) {
  return (
    <>
      <SidebarMenuButton
        aria-disabled
        tooltip={`${item.label} · todavía no`}
        className="hover:text-sidebar-foreground cursor-default opacity-55 hover:bg-transparent aria-disabled:pointer-events-auto"
      >
        <item.icon aria-hidden />
        <span>{item.label}</span>
      </SidebarMenuButton>
      <SidebarMenuBadge className="text-muted-foreground font-normal">
        pronto
      </SidebarMenuBadge>
    </>
  );
}
