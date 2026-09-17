"use client";

import { useCallback, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Building2, Check, ChevronsUpDown, LayoutGrid } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
import type { CurrentUser } from "@/features/auth/use-current-user";
import { PLAN_OPTIONS } from "@/features/tenants/options";
import { cn } from "@/lib/utils";
import type { Scope } from "@/lib/navigation";

interface OrgSwitcherProps {
  user: CurrentUser;
  scope: Scope;
}

export function planLabel(plan: string): string {
  return PLAN_OPTIONS.find((option) => option.value === plan)?.label ?? plan;
}

/** La organización dueña del tenant activo — `undefined` fuera de scope tenant. */
export function ownerOrg(user: CurrentUser, scope: Scope) {
  if (scope.kind === "tenant") {
    return user.organizations.find((org) =>
      org.tenants.some((tenant) => tenant.tenantId === scope.tenantId),
    );
  }
  if (scope.kind === "organization") {
    return user.organizations.find(
      (org) => org.organizationSlug === scope.orgSlug,
    );
  }
  return undefined;
}

interface OrgPickerListProps {
  user: CurrentUser;
  scope: Scope;
  activeOrgId?: string;
  /** Se llama después de navegar — cierra el popover (desktop) o el sheet (mobile). */
  onSelect: () => void;
}

/**
 * La lista de organizaciones del usuario, buscable, más "Todas las
 * organizaciones" — el contenido que comparten el popover de escritorio
 * (`OrgSwitcher`) y la pestaña "Organización" del sheet mobile
 * (`WorkspaceSwitcherMobile`). Sin trigger propio.
 *
 * Elegir una organización aterriza en su primer tenant si ya estabas en el
 * espacio de trabajo de un tenant —así no se cae a una pantalla vacía— o en
 * `/org/{slug}` si estabas navegando a nivel de organización.
 */
export function OrgPickerList({
  user,
  scope,
  activeOrgId,
  onSelect,
}: OrgPickerListProps) {
  const router = useRouter();

  const goToOrg = useCallback(
    (orgId: string) => {
      onSelect();

      const org = user.organizations.find((o) => o.organizationId === orgId);
      if (!org) return;

      if (scope.kind === "tenant") {
        const firstTenant = org.tenants[0];
        router.push(
          firstTenant
            ? `/tenant/${firstTenant.tenantId}`
            : `/org/${org.organizationSlug}`,
        );
        return;
      }

      router.push(`/org/${org.organizationSlug}`);
    },
    [router, scope, user.organizations, onSelect],
  );

  return (
    <Command>
      <CommandInput
        placeholder="Buscar organización…"
        className="h-9 text-xs"
      />
      <CommandList>
        <CommandEmpty className="text-muted-foreground py-4 text-center text-xs">
          No encontramos nada.
        </CommandEmpty>
        <CommandGroup>
          {user.organizations.map((org) => (
            <CommandItem
              key={org.organizationId}
              onSelect={() => goToOrg(org.organizationId)}
              className="flex cursor-pointer items-center gap-2 px-2.5 py-1.5 text-xs"
            >
              <Building2
                className="text-muted-foreground size-3.5 shrink-0"
                aria-hidden
              />
              <span className="flex-1 truncate font-medium">
                {org.organizationName}
              </span>
              <Badge
                variant="outline"
                className="text-muted-foreground h-4 px-1 font-mono text-[10px] uppercase"
              >
                {planLabel(org.plan)}
              </Badge>
              <Check
                aria-hidden
                className={cn(
                  "text-primary size-3.5 shrink-0 transition-opacity",
                  org.organizationId === activeOrgId
                    ? "opacity-100"
                    : "opacity-0",
                )}
              />
            </CommandItem>
          ))}
        </CommandGroup>

        <CommandSeparator />

        <CommandGroup>
          <CommandItem
            className="text-muted-foreground hover:text-foreground flex cursor-pointer items-center gap-2 px-2.5 py-1.5 text-xs"
            onSelect={() => {
              onSelect();
              router.push("/organizaciones");
            }}
          >
            <LayoutGrid className="size-3.5 shrink-0" aria-hidden />
            <span>Todas las organizaciones</span>
          </CommandItem>
        </CommandGroup>
      </CommandList>
    </Command>
  );
}

/**
 * El primer segmento del topbar: qué organización estás mirando, con un
 * badge de plan — patrón "split button" (Supabase). Escondido en mobile
 * (`WorkspaceSwitcherMobile` lo reemplaza ahí, ver `app-topbar.tsx`).
 */
export function OrgSwitcher({ user, scope }: OrgSwitcherProps) {
  const [open, setOpen] = useState(false);

  const active = ownerOrg(user, scope);

  if (user.organizations.length === 0) return null;

  return (
    <div className="border-border/60 bg-background hover:border-border/90 flex h-8 items-center rounded-lg border shadow-2xs transition-colors">
      {/* Lado Izquierdo: Link directo a la Organización */}
      {active ? (
        <Link
          href={`/org/${active.organizationSlug}`}
          className="hover:bg-accent/80 hover:text-accent-foreground flex h-full items-center gap-1.5 rounded-l-[7px] px-2.5 text-xs font-medium transition-colors"
        >
          <Building2
            className="text-muted-foreground size-3.5 shrink-0"
            aria-hidden
          />
          <span className="max-w-36 truncate">{active.organizationName}</span>
          <Badge
            variant="secondary"
            className="border-border/40 bg-muted/60 text-muted-foreground h-4 px-1.5 font-mono text-[10px] font-semibold tracking-wide uppercase"
          >
            {planLabel(active.plan)}
          </Badge>
        </Link>
      ) : (
        <span className="text-muted-foreground flex h-full items-center gap-1.5 px-2.5 text-xs">
          <Building2 className="size-3.5 shrink-0" aria-hidden />
          Elegí una organización
        </span>
      )}

      {/* Separador vertical sutil */}
      <div className="bg-border/60 h-3.5 w-px shrink-0" />

      {/* Lado Derecho: Desplegable (Combobox) */}
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger
          render={
            <Button
              variant="link"
              size="icon"
              className="hover:bg-accent/80 text-muted-foreground hover:text-foreground h-full w-6.5 rounded-l-none rounded-r-[7px] p-0 transition-colors"
              aria-label="Cambiar de organización"
            />
          }
        >
          <ChevronsUpDown className="size-3.5" aria-hidden />
        </PopoverTrigger>

        <PopoverContent
          className="w-72 p-0"
          side="bottom"
          align="start"
          sideOffset={6}
        >
          <OrgPickerList
            user={user}
            scope={scope}
            activeOrgId={active?.organizationId}
            onSelect={() => setOpen(false)}
          />
        </PopoverContent>
      </Popover>
    </div>
  );
}
