"use client";

import { ArrowDown, ArrowUp, ChevronsUpDown } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/**
 * Lo mínimo que el encabezado necesita de la columna. Se declara así en lugar de importar
 * el tipo completo de la columna porque este componente no usa nada más, y el tipo real
 * arrastra los seis parámetros genéricos de v9.
 */
interface SortableColumn {
  getCanSort: () => boolean;
  getIsSorted: () => false | "asc" | "desc";
  getToggleSortingHandler: () => ((event: unknown) => void) | undefined;
}

interface DataTableColumnHeaderProps {
  title: string;
  /**
   * La columna, tal como llega al callback `header`. Sin ella el encabezado es texto plano.
   *
   * La columna se maneja sola: lee su propio estado de orden y lo cambia con su propio
   * manejador. Así las columnas se definen una vez, fuera del componente, y no hay que
   * pasarles el estado por props hasta el fondo.
   */
  column?: SortableColumn;
  className?: string;
}

/**
 * Encabezado de columna, ordenable cuando la columna lo permite.
 *
 * Cicla en **tres** estados —ascendente, descendente, sin orden— porque volver a «sin
 * orden» sin recargar la página es lo que uno espera y casi ninguna tabla ofrece.
 *
 * El orden lo aplica el **servidor**: el cambio va a la URL y de ahí al parámetro `sort`
 * de la API. Ordenar en el cliente solo ordenaría las 20 filas que ya llegaron, y el
 * listado mentiría.
 */
export function DataTableColumnHeader({
  title,
  column,
  className,
}: DataTableColumnHeaderProps) {
  if (!column?.getCanSort()) {
    return (
      <span className={cn("text-muted-foreground font-medium", className)}>
        {title}
      </span>
    );
  }

  const sorted = column.getIsSorted();
  const Icon =
    sorted === "asc" ? ArrowUp : sorted === "desc" ? ArrowDown : ChevronsUpDown;

  return (
    <Button
      variant="ghost"
      size="sm"
      onClick={column.getToggleSortingHandler()}
      className={cn("-ml-2 h-8 gap-1.5 px-2 font-medium", className)}
      data-active={sorted !== false}
      aria-label={`Ordenar por ${title}`}
    >
      {title}
      <Icon
        className={cn(
          "size-3.5 shrink-0",
          sorted === false ? "text-muted-foreground/60" : "text-foreground",
        )}
        aria-hidden
      />
    </Button>
  );
}
