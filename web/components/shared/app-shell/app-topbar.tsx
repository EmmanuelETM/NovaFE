"use client";

import { Bell, Search } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Kbd, KbdGroup } from "@/components/ui/kbd";
import { Separator } from "@/components/ui/separator";
import { SidebarTrigger } from "@/components/ui/sidebar";
import type { CurrentUser } from "@/features/auth/use-current-user";
import type { Scope } from "@/lib/navigation";
import { useCommandPaletteStore } from "@/lib/stores/command-palette";

import { OrgSwitcher } from "./org-switcher";
import { TenantSwitcher } from "./tenant-switcher";

interface AppTopbarProps {
  user: CurrentUser;
  scope: Scope;
}

/**
 * La barra superior: los selectores de contexto (Organización ▾ / Tenant ▾)
 * y los accesos globales (buscar, notificaciones). Sin miga de pan de
 * página — el `h1` de cada pantalla (`PageHeader`) ya dice qué se está
 * viendo, repetirlo acá era ruido visual.
 *
 * Los switchers son el **único** lugar para cambiar de organización/tenant —
 * el pie del sidebar (`NavUser`) ya no tiene nada de eso, es solo la sesión.
 * `TenantSwitcher` no se pinta fuera de scope tenant: no hay un tenant activo
 * del que colgar el segundo segmento.
 *
 * No se queda fija con `sticky`: quien tiene el scroll es el panel de contenido, así que
 * esta barra está quieta por construcción. Con `sticky` se pegaría al borde de la ventana y
 * no al del panel —que con `variant="inset"` está dos píxeles más adentro—, y se vería
 * flotando sobre la esquina redondeada.
 */
export function AppTopbar({ user, scope }: AppTopbarProps) {
  const setPaletteOpen = useCommandPaletteStore((state) => state.setOpen);

  return (
    <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
      <SidebarTrigger className="-ml-1" />
      <Separator orientation="vertical" className="mr-1 !h-4" />

      {scope.kind !== "operator" && (
        <>
          <OrgSwitcher user={user} scope={scope} />
          {scope.kind === "tenant" && (
            <>
              <span className="text-muted-foreground/50 text-sm">/</span>
              <TenantSwitcher user={user} tenantId={scope.tenantId} />
            </>
          )}
        </>
      )}

      <div className="ml-auto flex items-center gap-1">
        <Button
          variant="outline"
          size="sm"
          className="text-muted-foreground h-8 gap-2 font-normal"
          onClick={() => setPaletteOpen(true)}
        >
          <Search className="size-4" aria-hidden />
          <span className="hidden sm:inline">Buscar</span>
          <KbdGroup className="hidden sm:inline-flex">
            <Kbd>⌘</Kbd>
            <Kbd>K</Kbd>
          </KbdGroup>
        </Button>

        <Button
          variant="ghost"
          size="icon"
          className="text-muted-foreground size-8"
          disabled
          title="Notificaciones — pronto"
        >
          <Bell className="size-4" aria-hidden />
        </Button>
      </div>
    </header>
  );
}
