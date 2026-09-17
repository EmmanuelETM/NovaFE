"use client";

import { useState } from "react";
import { usePathname } from "next/navigation";
import { Building, Building2, ChevronDown } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Drawer,
  DrawerContent,
  DrawerDescription,
  DrawerHeader,
  DrawerTitle,
  DrawerTrigger,
} from "@/components/ui/drawer";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { CurrentUser } from "@/features/auth/use-current-user";
import type { Scope } from "@/lib/navigation";

import { OrgPickerList, ownerOrg } from "./org-switcher";
import { TenantPickerList } from "./tenant-switcher";

interface WorkspaceSwitcherMobileProps {
  user: CurrentUser;
  scope: Scope;
}

/**
 * El equivalente mobile de `OrgSwitcher`+`TenantSwitcher` juntos — en una
 * pantalla angosta el par de split-buttons no entra (se salía de la
 * pantalla). Un solo trigger compacto abre un `Drawer` (shadcn sobre
 * `@base-ui/react/drawer` — no `vaul`, mismo criterio de Base UI que el
 * resto del proyecto) deslizable desde abajo, con pestañas
 * "Tenant"/"Organización", reutilizando las mismas listas
 * (`TenantPickerList`/`OrgPickerList`) que ya usan los popovers de
 * escritorio — mismo dato, misma lógica de navegación, solo cambia el
 * contenedor.
 *
 * Visible solo bajo `sm:hidden` en `AppTopbar`; los switchers de escritorio
 * llevan `hidden sm:flex` — nunca están los dos montados a la vez por CSS
 * puro, sin depender de `useIsMobile()` (evita el parpadeo de hidratación).
 */
export function WorkspaceSwitcherMobile({
  user,
  scope,
}: WorkspaceSwitcherMobileProps) {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);

  if (scope.kind === "operator" || user.organizations.length === 0) return null;

  const active = ownerOrg(user, scope);
  const nombre =
    scope.kind === "tenant"
      ? (user.tenantName ?? "Tu contribuyente")
      : (active?.organizationName ?? "Elegí una organización");

  return (
    <Drawer open={open} onOpenChange={setOpen} showSwipeHandle>
      <DrawerTrigger
        render={
          <Button
            variant="outline"
            size="sm"
            className="border-border/60 h-8 max-w-48 gap-1.5 px-2.5 text-xs font-medium shadow-2xs"
          />
        }
      >
        {scope.kind === "tenant" ? (
          <Building
            className="text-muted-foreground size-3.5 shrink-0"
            aria-hidden
          />
        ) : (
          <Building2
            className="text-muted-foreground size-3.5 shrink-0"
            aria-hidden
          />
        )}
        <span className="truncate">{nombre}</span>
        <ChevronDown
          className="text-muted-foreground size-3.5 shrink-0"
          aria-hidden
        />
      </DrawerTrigger>

      <DrawerContent
        // Sin foco automático: el input de búsqueda es lo primero enfocable,
        // y auto-enfocarlo abre el teclado apenas se desliza el drawer — con
        // la animación de entrada, se siente como un salto. Que el usuario
        // lo pida con un toque.
        initialFocus={false}
      >
        <DrawerHeader className="sr-only">
          <DrawerTitle>Cambiar de espacio de trabajo</DrawerTitle>
          <DrawerDescription>
            Elegí un tenant o una organización.
          </DrawerDescription>
        </DrawerHeader>

        {scope.kind === "tenant" ? (
          <Tabs
            defaultValue="tenant"
            className="flex min-h-0 flex-1 flex-col px-4 pb-4"
          >
            <TabsList className="mt-2 w-full">
              <TabsTrigger value="tenant">
                <Building className="size-3.5" aria-hidden />
                Tenant
              </TabsTrigger>
              <TabsTrigger value="organizacion">
                <Building2 className="size-3.5" aria-hidden />
                Organización
              </TabsTrigger>
            </TabsList>

            <TabsContent value="tenant" className="min-h-0">
              <TenantPickerList
                user={user}
                tenantId={scope.tenantId}
                pathname={pathname}
                onSelect={() => setOpen(false)}
              />
            </TabsContent>

            <TabsContent value="organizacion" className="min-h-0">
              <OrgPickerList
                user={user}
                scope={scope}
                activeOrgId={active?.organizationId}
                onSelect={() => setOpen(false)}
              />
            </TabsContent>
          </Tabs>
        ) : (
          <div className="min-h-0 flex-1 px-4 pb-4">
            <OrgPickerList
              user={user}
              scope={scope}
              activeOrgId={active?.organizationId}
              onSelect={() => setOpen(false)}
            />
          </div>
        )}
      </DrawerContent>
    </Drawer>
  );
}
