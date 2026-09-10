"use client";

import { Check, ChevronsUpDown } from "lucide-react";

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
import { cn } from "@/lib/utils";

import { useTenantOptions } from "./use-tenant-options";

interface TenantComboboxProps {
  value: string | null;
  onChange: (tenantId: string | null) => void;
}

/** Selector de contribuyente (búsqueda por RNC o razón social, filtrado en cliente). */
export function TenantCombobox({ value, onChange }: TenantComboboxProps) {
  const { data: tenants = [], isPending } = useTenantOptions();
  const selected = tenants.find((tenant) => tenant.id === value);

  return (
    <Popover>
      {/* Base UI usa `render`, no `asChild`. */}
      <PopoverTrigger
        render={
          <Button
            variant="outline"
            className="w-80 justify-between font-normal"
          />
        }
      >
        <span className="truncate">
          {selected
            ? `${selected.legalName} · ${selected.rnc}`
            : "Elegí un contribuyente…"}
        </span>
        <ChevronsUpDown className="opacity-50" aria-hidden />
      </PopoverTrigger>

      <PopoverContent className="w-80 p-0" align="start">
        <Command>
          <CommandInput placeholder="RNC o razón social…" className="h-9" />
          <CommandList>
            <CommandEmpty>
              {isPending ? "Cargando…" : "Sin resultados."}
            </CommandEmpty>
            <CommandGroup>
              {tenants.map((tenant) => (
                <CommandItem
                  key={tenant.id}
                  value={`${tenant.legalName} ${tenant.rnc}`}
                  onSelect={() =>
                    onChange(tenant.id === value ? null : tenant.id)
                  }
                >
                  <Check
                    className={cn(
                      "size-4 shrink-0",
                      tenant.id === value ? "opacity-100" : "opacity-0",
                    )}
                    aria-hidden
                  />
                  <span className="flex min-w-0 flex-col">
                    <span className="truncate">{tenant.legalName}</span>
                    <span className="text-muted-foreground text-xs">
                      {tenant.rnc}
                    </span>
                  </span>
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
