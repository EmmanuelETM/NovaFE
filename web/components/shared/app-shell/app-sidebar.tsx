import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuItem,
  SidebarRail,
} from "@/components/ui/sidebar";
import { roleRank } from "@/features/auth/roles";
import type { CurrentUser } from "@/features/auth/use-current-user";

import { Brand } from "./brand";
import { NavMain } from "./nav-main";
import { NavUser } from "./nav-user";

interface AppSidebarProps {
  user: CurrentUser;
}

/**
 * La navegación principal.
 *
 * `collapsible="icon"` en lugar de esconderse del todo: el punto de venta necesita el
 * ancho, y colapsar a iconos lo da sin dejar al usuario sin forma de salir de la pantalla.
 * El estado se recuerda en una cookie, así que quien lo colapsa lo encuentra colapsado
 * mañana.
 */
export function AppSidebar({ user }: AppSidebarProps) {
  return (
    <Sidebar collapsible="icon" variant="inset">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <Brand />
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        <NavMain role={roleRank(user.role)} />
      </SidebarContent>

      <SidebarFooter>
        <NavUser user={user} />
      </SidebarFooter>

      {/* El borde arrastrable: colapsar sin apuntarle al botón. */}
      <SidebarRail />
    </Sidebar>
  );
}
