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

interface ComboboxProps<T> {
  options: readonly T[];
  value: string;
  onChange: (value: string) => void;
  getValue: (option: T) => string;
  getLabel: (option: T) => string;
  /** Texto opcional bajo la etiqueta principal (p. ej. una categoría). */
  getDescription?: (option: T) => string | undefined;
  placeholder: string;
  searchPlaceholder: string;
  emptyText: string;
  disabled?: boolean;
  className?: string;
}

/**
 * Selector con búsqueda de texto, para catálogos que son demasiado largos
 * para un `Select` (el usuario tiene que poder escribir el nombre en vez de
 * desplazarse). Mismo patrón que `features/users/tenant-combobox.tsx`
 * (Popover + Command de shadcn), generalizado — genérico en `T` para no
 * repetir el mismo Popover+Command cada vez que aparece un catálogo nuevo.
 */
export function Combobox<T>({
  options,
  value,
  onChange,
  getValue,
  getLabel,
  getDescription,
  placeholder,
  searchPlaceholder,
  emptyText,
  disabled,
  className,
}: ComboboxProps<T>) {
  const selected = options.find((option) => getValue(option) === value);

  return (
    <Popover>
      {/* Base UI usa `render`, no `asChild`. */}
      <PopoverTrigger
        disabled={disabled}
        render={
          <Button
            variant="outline"
            className={cn("w-full justify-between font-normal", className)}
          />
        }
      >
        <span className="truncate">
          {selected ? getLabel(selected) : placeholder}
        </span>
        <ChevronsUpDown className="opacity-50" aria-hidden />
      </PopoverTrigger>

      <PopoverContent className="w-80 max-w-[90vw] p-0" align="start">
        <Command>
          <CommandInput placeholder={searchPlaceholder} className="h-9" />
          <CommandList>
            <CommandEmpty>{emptyText}</CommandEmpty>
            <CommandGroup>
              {options.map((option) => {
                const optionValue = getValue(option);
                const description = getDescription?.(option);
                return (
                  <CommandItem
                    key={optionValue}
                    value={`${getLabel(option)} ${optionValue}`}
                    onSelect={() =>
                      onChange(optionValue === value ? "" : optionValue)
                    }
                  >
                    <Check
                      className={cn(
                        "size-4 shrink-0",
                        optionValue === value ? "opacity-100" : "opacity-0",
                      )}
                      aria-hidden
                    />
                    <span className="flex min-w-0 flex-col">
                      <span className="truncate">{getLabel(option)}</span>
                      {description && (
                        <span className="text-muted-foreground text-xs">
                          {description}
                        </span>
                      )}
                    </span>
                  </CommandItem>
                );
              })}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
