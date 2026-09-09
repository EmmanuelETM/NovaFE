"use client";

import { Check, Filter } from "lucide-react";

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
import { Separator } from "@/components/ui/separator";
import { cn } from "@/lib/utils";

import type { DataTableFilter } from "./types";

interface DataTableFacetedFilterProps {
  filter: DataTableFilter;
  value: string | null;
  onChange: (value: string | null) => void;
}

/**
 * Un filtro en popover.
 *
 * En popover y no en una barra siempre visible por una razón de espacio: seis filtros
 * ocupan una franja permanente que se mira una vez al día. El popover deja el espacio para
 * los datos, y lo que sí queda a la vista es el valor **activo**, en el propio botón.
 *
 * Selección simple, porque es lo que aceptan los endpoints: `categoryId` es un valor, no
 * una lista. Si algún día alguno acepta varios, este es el único archivo que cambia.
 */
export function DataTableFacetedFilter({
  filter,
  value,
  onChange,
}: DataTableFacetedFilterProps) {
  const selected = filter.options.find((option) => option.value === value);

  return (
    <Popover>
      {/* Base UI usa `render`, no `asChild`: el elemento va en la prop y el contenido
          como hijos. Con `asChild` no compila. */}
      <PopoverTrigger
        render={
          <Button
            variant="outline"
            size="sm"
            className="h-8 gap-1.5 border-dashed"
          />
        }
      >
        <Filter className="size-3.5" aria-hidden />
        {filter.label}
        {selected && (
          <>
            <Separator orientation="vertical" className="mx-0.5 h-4" />
            <Badge variant="secondary" className="rounded px-1.5 font-normal">
              {selected.label}
            </Badge>
          </>
        )}
      </PopoverTrigger>

      <PopoverContent className="w-[220px] p-0" align="start">
        <Command>
          {filter.options.length > 8 && (
            <CommandInput placeholder={filter.label} className="h-9" />
          )}

          <CommandList>
            <CommandEmpty>Sin resultados.</CommandEmpty>

            <CommandGroup>
              {filter.options.map((option) => {
                const isSelected = option.value === value;

                return (
                  <CommandItem
                    key={option.value}
                    onSelect={() => onChange(isSelected ? null : option.value)}
                  >
                    <Check
                      className={cn(
                        "size-4",
                        isSelected ? "opacity-100" : "opacity-0",
                      )}
                      aria-hidden
                    />
                    <span>{option.label}</span>
                  </CommandItem>
                );
              })}
            </CommandGroup>

            {selected && (
              <>
                <CommandSeparator />
                <CommandGroup>
                  <CommandItem
                    onSelect={() => onChange(null)}
                    className="justify-center text-sm"
                  >
                    Quitar filtro
                  </CommandItem>
                </CommandGroup>
              </>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
