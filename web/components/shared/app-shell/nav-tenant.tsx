"use client";

import Link from "next/link";
import { Building, ChevronsUpDown } from "lucide-react";

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from "@/components/ui/sidebar";
import { roleRank } from "@/features/auth/roles";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { NAVIGATION, visibleNavItems } from "@/lib/navigation";

interface NavTenantProps {
  user: CurrentUser;
}

/**
 * Lo del contribuyente, al pie del sidebar: empresa, certificados,
 * secuencias, webhooks, plan… Deliberadamente separado de `NavUser` (que
 * vive en la barra superior): una cosa es quién sos vos, otra es la empresa
 * que estás administrando — mismo criterio que Vercel (Team) o Linear
 * (Workspace) usan para no mezclar la cuenta personal con la organización.
 *
 * No se pinta para un operador (`tenantId` null: no administra un
 * contribuyente propio) ni para `consultor`/`emisor` (ningún ítem de esta
 * sección los admite) — mostrar un menú sin nada útil adentro es peor que
 * no mostrarlo.
 */
export function NavTenant({ user }: NavTenantProps) {
  const { isMobile } = useSidebar();

  if (!user.tenantId) return null;

  const section = NAVIGATION.find((s) => s.hideFromSidebar);
  const items = section
    ? visibleNavItems(section.items, roleRank(user.role))
    : [];

  if (items.length === 0) return null;

  const nombre = user.tenantName ?? "Tu contribuyente";

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={<SidebarMenuButton size="lg" tooltip={nombre} />}
          >
            <div className="bg-muted text-muted-foreground flex size-7 shrink-0 items-center justify-center rounded-lg">
              <Building className="size-4" aria-hidden />
            </div>
            <div className="grid flex-1 text-left leading-tight">
              <span className="truncate text-sm font-medium">{nombre}</span>
              <span className="text-muted-foreground truncate text-xs">
                Tu empresa
              </span>
            </div>
            <ChevronsUpDown className="ml-auto" aria-hidden />
          </DropdownMenuTrigger>

          <DropdownMenuContent
            className="w-(--anchor-width) min-w-56"
            side={isMobile ? "bottom" : "right"}
            align="end"
            sideOffset={8}
          >
            <DropdownMenuGroup>
              <DropdownMenuLabel className="text-muted-foreground text-xs">
                {nombre}
              </DropdownMenuLabel>
            </DropdownMenuGroup>

            <DropdownMenuSeparator />

            <DropdownMenuGroup>
              {items.map((item) =>
                item.ready ? (
                  <DropdownMenuItem
                    key={item.href}
                    render={<Link href={item.href} />}
                  >
                    <item.icon aria-hidden />
                    {item.label}
                  </DropdownMenuItem>
                ) : (
                  <DropdownMenuItem key={item.href} disabled>
                    <item.icon aria-hidden />
                    {item.label}
                    <span className="text-muted-foreground ml-auto text-xs font-normal">
                      pronto
                    </span>
                  </DropdownMenuItem>
                ),
              )}
            </DropdownMenuGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}
