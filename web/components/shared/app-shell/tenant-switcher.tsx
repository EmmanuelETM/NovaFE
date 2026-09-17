"use client";

import { useCallback, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { Building, Check, ChevronsUpDown } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { useEmitterProfile } from "@/features/tenant-config/use-emitter-profile";
import { ENVIRONMENT_OPTIONS } from "@/features/tenants/options";
import { cn } from "@/lib/utils";

interface TenantSwitcherProps {
  user: CurrentUser;
  tenantId: string;
}

/** A dónde aterriza `/` por defecto la próxima vez — nunca decide qué tenant está activo en una pantalla ya cargada, eso lo da la URL. */
const LAST_TENANT_COOKIE = "last_tenant_id";
const LAST_TENANT_MAX_AGE = 60 * 60 * 24 * 30; // 30 días

const ENVIRONMENT_COLOR: Record<string, string> = {
  Test: "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400",
  Cert: "border-sky-500/30 bg-sky-500/10 text-sky-700 dark:text-sky-400",
  Production:
    "border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400",
};

function environmentLabel(value: string): string {
  return (
    ENVIRONMENT_OPTIONS.find((option) => option.value === value)?.label ?? value
  );
}

/** Reemplaza el `[tenantId]` de la ruta actual, conservando la sub-ruta (`/comprobantes`, etc.). */
function withTenant(pathname: string, tenantId: string): string {
  return pathname.startsWith("/tenant/")
    ? pathname.replace(/^\/tenant\/[^/]+/, `/tenant/${tenantId}`)
    : `/tenant/${tenantId}`;
}

interface TenantPickerListProps {
  user: CurrentUser;
  tenantId: string;
  pathname: string;
  /** Se llama después de navegar — cierra el popover (desktop) o el sheet (mobile). */
  onSelect: () => void;
}

/**
 * La lista de tenants de la organización activa, buscable — el contenido
 * que comparten el popover de escritorio (`TenantSwitcher`) y la pestaña
 * "Tenant" del sheet mobile (`WorkspaceSwitcherMobile`). Sin trigger propio:
 * quien la usa decide cómo se abre.
 */
export function TenantPickerList({
  user,
  tenantId,
  pathname,
  onSelect,
}: TenantPickerListProps) {
  const router = useRouter();

  const currentOrg = user.organizations.find((org) =>
    org.tenants.some((tenant) => tenant.tenantId === tenantId),
  );

  const rememberAndGo = useCallback(
    (path: string, rememberTenantId: string) => {
      onSelect();

      try {
        document.cookie = `${LAST_TENANT_COOKIE}=${rememberTenantId}; path=/; max-age=${LAST_TENANT_MAX_AGE}`;
      } catch {
        // Almacenamiento bloqueado (ventana privada, etc.): solo se pierde
        // el atajo de "último tenant" al volver a `/`, nada crítico.
      }

      router.push(path);
    },
    [router, onSelect],
  );

  if (!currentOrg) return null;

  return (
    <Command>
      <CommandInput placeholder="Buscar tenant…" />
      <CommandList>
        <CommandEmpty>No encontramos nada.</CommandEmpty>
        <CommandGroup heading={currentOrg.organizationName}>
          {currentOrg.tenants.map((tenant) => (
            <CommandItem
              key={tenant.tenantId}
              onSelect={() =>
                rememberAndGo(
                  withTenant(pathname, tenant.tenantId),
                  tenant.tenantId,
                )
              }
            >
              <Building aria-hidden />
              <span className="flex-1 truncate">{tenant.tenantName}</span>
              <Check
                aria-hidden
                className={cn(
                  "size-4",
                  tenant.tenantId === tenantId ? "opacity-100" : "opacity-0",
                )}
              />
            </CommandItem>
          ))}
        </CommandGroup>
      </CommandList>
    </Command>
  );
}

/**
 * El segundo segmento del breadcrumb del topbar: qué tenant, dentro de la
 * organización activa, con un badge de ambiente DGII (`Test`/`Cert`/
 * `Production` — nunca solo dos valores). Solo se pinta en scope tenant.
 * Escondido en mobile (`WorkspaceSwitcherMobile` lo reemplaza ahí, ver
 * `app-topbar.tsx`) — el par de switchers no entra en una pantalla angosta.
 *
 * A diferencia del viejo `NavTenant` (pie del sidebar), este componente
 * **únicamente** cambia de tenant — nada de Certificados/Secuencias/
 * Webhooks/Configuración, que ahora viven en el sidebar categorizado.
 */
export function TenantSwitcher({ user, tenantId }: TenantSwitcherProps) {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const { data: profile } = useEmitterProfile();

  const nombre = user.tenantName ?? "Tu contribuyente";

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        render={
          <Button
            variant="ghost"
            size="sm"
            className="h-8 gap-1.5 px-2 font-normal"
          />
        }
      >
        <Building className="text-muted-foreground size-4" aria-hidden />
        <span className="max-w-40 truncate text-sm">{nombre}</span>
        {profile && (
          <Badge
            variant="outline"
            className={cn(
              "font-normal",
              ENVIRONMENT_COLOR[profile.defaultEnvironment],
            )}
          >
            {environmentLabel(profile.defaultEnvironment)}
          </Badge>
        )}
        <ChevronsUpDown
          className="text-muted-foreground size-3.5"
          aria-hidden
        />
      </PopoverTrigger>

      <PopoverContent
        className="w-72 p-0"
        side="bottom"
        align="start"
        sideOffset={8}
      >
        <TenantPickerList
          user={user}
          tenantId={tenantId}
          pathname={pathname}
          onSelect={() => setOpen(false)}
        />
      </PopoverContent>
    </Popover>
  );
}
