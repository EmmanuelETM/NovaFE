"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Boxes, Building, Building2 } from "lucide-react";

import {
  Command,
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from "@/components/ui/command";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { roleRank } from "@/features/auth/roles";
import { useOrganizations } from "@/features/organizations/use-organizations";
import { useTenants } from "@/features/tenants/use-tenants";
import {
  NAVIGATION,
  scopedHref,
  visibleNavItems,
  type Scope,
} from "@/lib/navigation";
import { useCommandPaletteStore } from "@/lib/stores/command-palette";
import { useDebouncedValue } from "@/lib/use-debounced-value";

/** Cuántas filas trae cada grupo de búsqueda del operador. Una paleta no es un listado. */
const OPERATOR_SEARCH_PAGE_SIZE = 5;
const MIN_SEARCH_LENGTH = 2;

interface CommandPaletteProps {
  user: CurrentUser;
  scope: Scope;
}

/**
 * `⌘K`/`Ctrl+K`: salta entre las pantallas del scope activo y cambia de
 * tenant/organización. **Sin** buscador de datos (comprobantes, clientes…) —
 * eso necesitaría un backend de búsqueda que no existe todavía; esto es
 * puramente navegación, con lo que ya trae `/users/me`.
 */
export function CommandPalette({ user, scope }: CommandPaletteProps) {
  const router = useRouter();
  const open = useCommandPaletteStore((state) => state.open);
  const setOpen = useCommandPaletteStore((state) => state.setOpen);
  const [query, setQuery] = useState("");
  const debouncedQuery = useDebouncedValue(query, 250);

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key.toLowerCase() === "k" && (event.metaKey || event.ctrlKey)) {
        event.preventDefault();
        setOpen(true);
      }
    }

    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [setOpen]);

  function go(path: string) {
    setOpen(false);
    setQuery("");
    router.push(path);
  }

  const role = roleRank(user.role);
  const destinos = NAVIGATION.filter(
    (section) => (section.scope ?? "tenant") === scope.kind,
  )
    .flatMap((section) => visibleNavItems(section.items, role))
    .filter((item) => item.ready);

  // Búsqueda del operador (Fase 5): solo pega a la API con dos o más
  // caracteres, y solo en scope operador — el resto de scopes ya tiene todo
  // lo que necesita en `destinos`/`user.organizations`, sin backend de
  // búsqueda propio.
  const searching =
    scope.kind === "operator" &&
    debouncedQuery.trim().length >= MIN_SEARCH_LENGTH;

  const { data: tenantResults } = useTenants(
    { page: 1, pageSize: OPERATOR_SEARCH_PAGE_SIZE, search: debouncedQuery },
    searching,
  );
  const { data: organizationResults } = useOrganizations(
    { page: 1, pageSize: OPERATOR_SEARCH_PAGE_SIZE, search: debouncedQuery },
    searching,
  );
  const tenantMatches = tenantResults?.items ?? [];
  const organizationMatches = organizationResults?.items ?? [];

  return (
    <CommandDialog open={open} onOpenChange={setOpen}>
      <Command shouldFilter={!searching}>
        <CommandInput
          value={query}
          onValueChange={setQuery}
          placeholder="Buscar una pantalla, tenant u organización…"
        />
        <CommandList>
          <CommandEmpty>No encontramos nada.</CommandEmpty>

          {searching ? (
            <>
              {tenantMatches.length > 0 && (
                <CommandGroup heading="Contribuyentes">
                  {tenantMatches.map((tenant) => (
                    <CommandItem
                      key={tenant.id}
                      value={`tenant-${tenant.id}`}
                      onSelect={() => go(`/nemus/tenants/${tenant.id}`)}
                    >
                      <Building2 aria-hidden />
                      <div className="flex flex-col">
                        <span>{tenant.legalName}</span>
                        <span className="text-muted-foreground font-mono text-xs">
                          {tenant.rnc}
                        </span>
                      </div>
                    </CommandItem>
                  ))}
                </CommandGroup>
              )}

              {tenantMatches.length > 0 && organizationMatches.length > 0 && (
                <CommandSeparator />
              )}

              {organizationMatches.length > 0 && (
                <CommandGroup heading="Organizaciones">
                  {organizationMatches.map((organization) => (
                    <CommandItem
                      key={organization.id}
                      value={`organization-${organization.id}`}
                      onSelect={() =>
                        go(`/nemus/organizaciones/${organization.id}`)
                      }
                    >
                      <Boxes aria-hidden />
                      {organization.name}
                    </CommandItem>
                  ))}
                </CommandGroup>
              )}
            </>
          ) : (
            <>
              {destinos.length > 0 && (
                <CommandGroup heading="Ir a">
                  {destinos.map((item) => (
                    <CommandItem
                      key={item.href}
                      onSelect={() => go(scopedHref(scope, item.href))}
                    >
                      <item.icon aria-hidden />
                      {item.label}
                    </CommandItem>
                  ))}
                </CommandGroup>
              )}

              {user.organizations.length > 0 && (
                <>
                  <CommandSeparator />
                  {user.organizations.map((org) => (
                    <CommandGroup
                      key={org.organizationId}
                      heading={org.organizationName}
                    >
                      {org.tenants.map((tenant) => (
                        <CommandItem
                          key={tenant.tenantId}
                          onSelect={() => go(`/tenant/${tenant.tenantId}`)}
                        >
                          <Building aria-hidden />
                          {tenant.tenantName}
                        </CommandItem>
                      ))}
                      <CommandItem
                        onSelect={() => go(`/org/${org.organizationSlug}`)}
                      >
                        <Building2 aria-hidden />
                        Ver organización
                      </CommandItem>
                    </CommandGroup>
                  ))}
                </>
              )}
            </>
          )}
        </CommandList>
      </Command>
    </CommandDialog>
  );
}
