"use client";

import { Settings2 } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

import { useTableContext } from "./table-hook";

/**
 * Qué columnas se ven.
 *
 * Es estado de la tabla y no de la URL a propósito: es una preferencia personal, no parte
 * de la consulta. Compartir un enlace no debe imponerle al otro las columnas que uno
 * esconde.
 */
export function DataTableViewOptions() {
  const table = useTableContext();

  const columns = table
    .getAllLeafColumns()
    .filter((column) => column.getCanHide());

  if (columns.length === 0) return null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="outline" size="sm" className="ml-auto h-8 gap-1.5" />
        }
      >
        <Settings2 className="size-3.5" aria-hidden />
        <span className="hidden sm:inline">Columnas</span>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-[180px]">
        {/* El Group no es decorativo: en Base UI, GroupLabel exige un Group ancestro y
            sin él revienta en tiempo de ejecución con «MenuGroupContext is missing». Es
            otra diferencia con Radix, donde la etiqueta funciona suelta. */}
        <DropdownMenuGroup>
          <DropdownMenuLabel>Mostrar</DropdownMenuLabel>
          <DropdownMenuSeparator />

          {columns.map((column) => (
            <DropdownMenuCheckboxItem
              key={column.id}
              checked={column.getIsVisible()}
              onCheckedChange={(checked) =>
                column.toggleVisibility(Boolean(checked))
              }
            >
              {String(column.columnDef.meta?.label ?? column.id)}
            </DropdownMenuCheckboxItem>
          ))}
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
