"use client";

import { useCallback, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { Building, ChevronsUpDown, Settings } from "lucide-react";

import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from "@/components/ui/command";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from "@/components/ui/sidebar";
import { roleRank } from "@/features/auth/roles";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { NAVIGATION, tenantHref, visibleNavItems } from "@/lib/navigation";

interface NavTenantProps {
  user: CurrentUser;
}

/** A dónde aterriza `/` por defecto la próxima vez — nunca decide qué tenant está activo en una pantalla ya cargada, eso lo da la URL. */
const LAST_TENANT_COOKIE = "last_tenant_id";
const LAST_TENANT_MAX_AGE = 60 * 60 * 24 * 30; // 30 días

/** Reemplaza el `[tenantId]` de la ruta actual, conservando la sub-ruta (`/comprobantes`, etc.). */
function withTenant(pathname: string, tenantId: string): string {
  return pathname.startsWith("/tenant/")
    ? pathname.replace(/^\/tenant\/[^/]+/, `/tenant/${tenantId}`)
    : `/tenant/${tenantId}`;
}

/**
 * El switcher de organización/tenant, al pie del sidebar — al estilo
 * Supabase/Linear: un combobox con búsqueda, agrupado por organización.
 * Separado a propósito de `NavUser` (arriba a la derecha): una cosa es quién
 * sos, otra el contribuyente que estás administrando.
 *
 * También lleva los accesos de "Administración" (empresa, certificados,
 * secuencias, webhooks) del tenant activo — son del contribuyente, no del
 * día a día, así que no compiten por espacio con Inicio/Comprobantes en el
 * sidebar principal (`NavMain`, que oculta esa sección con
 * `hideFromSidebar`).
 *
 * No se pinta para un operador (`tenantId` null: no administra un
 * contribuyente propio).
 */
export function NavTenant({ user }: NavTenantProps) {
  const router = useRouter();
  const pathname = usePathname();
  const { isMobile } = useSidebar();
  const [open, setOpen] = useState(false);

  const tenantId = user.tenantId;

  const rememberAndGo = useCallback(
    (path: string, rememberTenantId?: string) => {
      setOpen(false);

      if (rememberTenantId) {
        try {
          document.cookie = `${LAST_TENANT_COOKIE}=${rememberTenantId}; path=/; max-age=${LAST_TENANT_MAX_AGE}`;
        } catch {
          // Almacenamiento bloqueado (ventana privada, etc.): solo se pierde
          // el atajo de "último tenant" al volver a `/`, nada crítico.
        }
      }

      router.push(path);
    },
    [router],
  );

  if (!tenantId) return null;

  const adminSection = NAVIGATION.find((s) => s.hideFromSidebar);
  const adminItems = adminSection
    ? visibleNavItems(adminSection.items, roleRank(user.role))
    : [];

  const nombre = user.tenantName ?? "Tu contribuyente";
  const currentOrg = user.organizations.find((org) =>
    org.tenants.some((tenant) => tenant.tenantId === tenantId),
  );

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <Popover open={open} onOpenChange={setOpen}>
          <PopoverTrigger
            render={
              <SidebarMenuButton size="lg" tooltip={nombre}>
                <div className="bg-muted text-muted-foreground flex size-7 shrink-0 items-center justify-center rounded-lg">
                  <Building className="size-4" aria-hidden />
                </div>
                <div className="grid flex-1 text-left leading-tight">
                  <span className="truncate text-sm font-medium">{nombre}</span>
                  <span className="text-muted-foreground truncate text-xs">
                    {currentOrg?.organizationName ?? "Tu empresa"}
                  </span>
                </div>
                <ChevronsUpDown className="ml-auto" aria-hidden />
              </SidebarMenuButton>
            }
          />

          <PopoverContent
            className="w-(--anchor-width) min-w-72 p-0"
            side={isMobile ? "bottom" : "right"}
            align="start"
            sideOffset={8}
          >
            <Command>
              <CommandInput placeholder="Buscar organización o tenant…" />
              <CommandList>
                <CommandEmpty>No encontramos nada.</CommandEmpty>

                {adminItems.length > 0 && (
                  <>
                    <CommandGroup heading="Administración">
                      {adminItems.map((item) => (
                        <CommandItem
                          key={item.href}
                          disabled={!item.ready}
                          onSelect={() => {
                            if (item.ready) {
                              rememberAndGo(tenantHref(tenantId, item.href));
                            }
                          }}
                        >
                          <item.icon aria-hidden />
                          {item.label}
                          {!item.ready && (
                            <span className="text-muted-foreground ml-auto text-xs">
                              pronto
                            </span>
                          )}
                        </CommandItem>
                      ))}
                    </CommandGroup>
                    <CommandSeparator />
                  </>
                )}

                {user.organizations.map((org, index) => (
                  <div key={org.organizationId}>
                    {index > 0 && <CommandSeparator />}
                    <CommandGroup heading={org.organizationName}>
                      {org.tenants.map((tenant) => (
                        <CommandItem
                          key={tenant.tenantId}
                          data-checked={tenant.tenantId === tenantId}
                          onSelect={() =>
                            rememberAndGo(
                              withTenant(pathname, tenant.tenantId),
                              tenant.tenantId,
                            )
                          }
                        >
                          <Building aria-hidden />
                          {tenant.tenantName}
                        </CommandItem>
                      ))}
                      <CommandItem
                        onSelect={() =>
                          rememberAndGo(`/org/${org.organizationSlug}`)
                        }
                      >
                        <Settings aria-hidden />
                        Ver organización
                      </CommandItem>
                    </CommandGroup>
                  </div>
                ))}
              </CommandList>
            </Command>
          </PopoverContent>
        </Popover>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}
