"use client";

import { useState } from "react";
import { Plus } from "lucide-react";

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
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { useTenants } from "@/features/tenants/use-tenants";
import { useDebouncedValue } from "@/lib/use-debounced-value";

import { useAssignTenantToOrganization } from "./use-organizations";

const PAGE_SIZE = 8;
const MIN_SEARCH_LENGTH = 2;

interface SelectedTenant {
  id: string;
  legalName: string;
  rnc: string;
}

/**
 * Asocia un tenant **existente** a la organización — el alta de un tenant
 * nuevo sigue siendo `/nemus/tenants/nuevo`, esto es para el caso "ya
 * existe, hay que engancharlo (o reengancharlo) a esta organización".
 */
export function AssignTenantDialog({
  organizationId,
}: {
  organizationId: string;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [selected, setSelected] = useState<SelectedTenant | null>(null);
  const debouncedQuery = useDebouncedValue(query, 250);
  const assign = useAssignTenantToOrganization(organizationId);

  const searching = debouncedQuery.trim().length >= MIN_SEARCH_LENGTH;
  const { data, isPending } = useTenants(
    { page: 1, pageSize: PAGE_SIZE, search: debouncedQuery },
    searching,
  );
  const results = data?.items ?? [];

  function close() {
    setOpen(false);
    setQuery("");
    setSelected(null);
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => (next ? setOpen(true) : close())}
    >
      <DialogTrigger
        render={<Button size="sm" variant="outline" className="h-8 gap-1.5" />}
      >
        <Plus /> Asociar tenant
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Asociar un tenant existente</DialogTitle>
          <DialogDescription>
            Busca por RNC o razón social. Si el tenant ya pertenece a otra
            organización, se reasigna a esta.
          </DialogDescription>
        </DialogHeader>

        <Command shouldFilter={false} className="rounded-lg border">
          <CommandInput
            value={query}
            onValueChange={(next) => {
              setQuery(next);
              setSelected(null);
            }}
            placeholder="RNC o razón social…"
          />
          <CommandList>
            {!searching ? (
              <CommandEmpty>Escribe al menos 2 caracteres.</CommandEmpty>
            ) : results.length === 0 && !isPending ? (
              <CommandEmpty>Sin resultados.</CommandEmpty>
            ) : (
              <CommandGroup>
                {results.map((tenant) => (
                  <CommandItem
                    key={tenant.id}
                    value={tenant.id}
                    onSelect={() => {
                      setSelected({
                        id: tenant.id,
                        legalName: tenant.legalName,
                        rnc: tenant.rnc,
                      });
                      setQuery(tenant.legalName);
                    }}
                  >
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
          </CommandList>
        </Command>

        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>
            Cancelar
          </DialogClose>
          <Button
            type="button"
            disabled={!selected || assign.isPending}
            onClick={async () => {
              if (!selected) return;
              await assign.mutateAsync(selected.id);
              close();
            }}
          >
            Asociar
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
