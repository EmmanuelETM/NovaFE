"use client";

import {
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { formatCount } from "@/lib/format";
import { selectItems } from "@/lib/select-items";

import type { DataTableSearchState, PagedResult } from "./types";
import { PAGE_SIZE_OPTIONS } from "./use-table-search-params";

/** Constante de módulo: recrearla en cada render daría una referencia nueva. */
const PAGE_SIZE_ITEMS = selectItems(
  PAGE_SIZE_OPTIONS.map((size) => ({
    value: String(size),
    label: String(size),
  })),
);

interface DataTablePaginationProps<TData> {
  page: PagedResult<TData> | undefined;
  state: DataTableSearchState;
  onStateChange: (patch: Partial<DataTableSearchState>) => void;
}

/**
 * Paginación del servidor.
 *
 * Los totales salen de `PagedResult` y no de contar las filas visibles: contar lo visible
 * daría «20 de 20» sobre quinientos registros.
 */
export function DataTablePagination<TData>({
  page,
  state,
  onStateChange,
}: DataTablePaginationProps<TData>) {
  const total = page?.totalCount ?? 0;
  const totalPages = Math.max(1, page?.totalPages ?? 1);
  const current = Math.min(state.page, totalPages);

  const from = total === 0 ? 0 : (current - 1) * state.pageSize + 1;
  const to = Math.min(current * state.pageSize, total);

  return (
    <div className="flex flex-col-reverse items-center justify-between gap-3 sm:flex-row">
      <p className="text-muted-foreground text-sm" aria-live="polite">
        {total === 0
          ? "Sin resultados"
          : `${formatCount(from)}–${formatCount(to)} de ${formatCount(total)}`}
      </p>

      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2">
          <span className="text-muted-foreground hidden text-sm sm:inline">
            Por página
          </span>
          <Select
            // Aquí la etiqueta es el propio número, pero el mapa va igual: la convención
            // es que todo Select lo lleve, para que el fallo no pueda volver por descuido.
            items={PAGE_SIZE_ITEMS}
            value={String(state.pageSize)}
            onValueChange={(value) =>
              onStateChange({ pageSize: Number(value) })
            }
          >
            <SelectTrigger
              size="sm"
              className="w-[72px]"
              aria-label="Filas por página"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PAGE_SIZE_OPTIONS.map((size) => (
                <SelectItem key={size} value={String(size)}>
                  {size}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="icon"
            className="size-8"
            onClick={() => onStateChange({ page: 1 })}
            disabled={current <= 1}
            aria-label="Primera página"
          >
            <ChevronsLeft className="size-4" aria-hidden />
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="size-8"
            onClick={() => onStateChange({ page: current - 1 })}
            disabled={current <= 1}
            aria-label="Página anterior"
          >
            <ChevronLeft className="size-4" aria-hidden />
          </Button>

          <span className="px-2 text-sm tabular-nums">
            {current} / {totalPages}
          </span>

          <Button
            variant="outline"
            size="icon"
            className="size-8"
            onClick={() => onStateChange({ page: current + 1 })}
            disabled={current >= totalPages}
            aria-label="Página siguiente"
          >
            <ChevronRight className="size-4" aria-hidden />
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="size-8"
            onClick={() => onStateChange({ page: totalPages })}
            disabled={current >= totalPages}
            aria-label="Última página"
          >
            <ChevronsRight className="size-4" aria-hidden />
          </Button>
        </div>
      </div>
    </div>
  );
}
