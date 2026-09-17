"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { Building, Building2 } from "lucide-react";

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
import {
  NAVIGATION,
  scopedHref,
  visibleNavItems,
  type Scope,
} from "@/lib/navigation";
import { useCommandPaletteStore } from "@/lib/stores/command-palette";

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
    router.push(path);
  }

  const role = roleRank(user.role);
  const destinos = NAVIGATION.filter(
    (section) => (section.scope ?? "tenant") === scope.kind,
  )
    .flatMap((section) => visibleNavItems(section.items, role))
    .filter((item) => item.ready);

  return (
    <CommandDialog open={open} onOpenChange={setOpen}>
      <Command>
        <CommandInput placeholder="Buscar una pantalla, tenant u organización…" />
        <CommandList>
          <CommandEmpty>No encontramos nada.</CommandEmpty>

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
        </CommandList>
      </Command>
    </CommandDialog>
  );
}
