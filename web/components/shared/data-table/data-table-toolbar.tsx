"use client";

import { useEffect, useState } from "react";
import { Search, X } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

import { DataTableFacetedFilter } from "./data-table-faceted-filter";
import { DataTableViewOptions } from "./data-table-view-options";
import type { DataTableFilter, DataTableSearchState } from "./types";

interface DataTableToolbarProps {
  state: DataTableSearchState;
  onStateChange: (patch: Partial<DataTableSearchState>) => void;
  filters: DataTableFilter[];
  searchPlaceholder: string;
  /** Si el endpoint acepta texto libre. Ver `searchable` en `DataTable`. */
  searchable: boolean;
  /** Acciones de la pantalla: «Nuevo producto». Van a la derecha. */
  actions?: React.ReactNode;
}

/** Cuánto se espera antes de consultar mientras se teclea. */
const SEARCH_DEBOUNCE_MS = 300;

/**
 * Buscador, filtros y opciones de columnas.
 *
 * El buscador tiene su propio estado local y **rebota** antes de tocar la URL: sin eso,
 * cada letra sería una petición y una entrada de historial. El estado local también es lo
 * que hace que el cursor no salte mientras se escribe.
 */
export function DataTableToolbar({
  state,
  onStateChange,
  filters,
  searchPlaceholder,
  searchable,
  actions,
}: DataTableToolbarProps) {
  const [search, setSearch] = useState(state.search);

  /**
   * Sincroniza el input cuando la URL cambia por fuera: el botón atrás, o «Limpiar».
   *
   * Se ajusta **durante el render** y no en un efecto. Es el patrón que React documenta
   * para esto, y el lint de React Compiler rechaza la alternativa con razón: un `setState`
   * dentro de un efecto provoca un render extra en cascada, y aquí ese render se notaría
   * como un parpadeo del texto al volver atrás.
   */
  const [lastExternalSearch, setLastExternalSearch] = useState(state.search);

  if (state.search !== lastExternalSearch) {
    setLastExternalSearch(state.search);
    setSearch(state.search);
  }

  // El rebote: sin él, cada letra sería una petición y una entrada de historial.
  useEffect(() => {
    if (search === state.search) return;

    const timer = setTimeout(
      () => onStateChange({ search }),
      SEARCH_DEBOUNCE_MS,
    );

    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const activeFilters = filters.filter((filter) => state.filters[filter.key]);
  const hasActive = activeFilters.length > 0 || state.search !== "";

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        {searchable && (
          <div className="relative w-full sm:w-[260px]">
            <Search
              className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2"
              aria-hidden
            />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={searchPlaceholder}
              className="h-8 pl-8"
              aria-label={searchPlaceholder}
            />
          </div>
        )}

        {filters.map((filter) => (
          <DataTableFacetedFilter
            key={filter.key}
            filter={filter}
            value={state.filters[filter.key] ?? null}
            onChange={(value) =>
              onStateChange({ filters: { [filter.key]: value } })
            }
          />
        ))}

        {hasActive && (
          <Button
            variant="ghost"
            size="sm"
            className="h-8 px-2"
            onClick={() =>
              onStateChange({
                search: "",
                filters: Object.fromEntries(filters.map((f) => [f.key, null])),
              })
            }
          >
            <X className="size-3.5" aria-hidden />
            Limpiar
          </Button>
        )}

        <div className="ml-auto flex items-center gap-2">
          <DataTableViewOptions />
          {actions}
        </div>
      </div>

      {/* Los filtros activos, como chips removibles: quitar uno no debe exigir volver a
          abrir su popover. */}
      {activeFilters.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5">
          {activeFilters.map((filter) => {
            const value = state.filters[filter.key];
            const option = filter.options.find((o) => o.value === value);

            return (
              <Badge
                key={filter.key}
                variant="secondary"
                className="gap-1 pr-1 font-normal"
              >
                <span className="text-muted-foreground">{filter.label}:</span>
                {option?.label ?? value}
                <button
                  type="button"
                  onClick={() =>
                    onStateChange({ filters: { [filter.key]: null } })
                  }
                  className="hover:bg-muted-foreground/20 ml-0.5 rounded p-0.5"
                  aria-label={`Quitar el filtro ${filter.label}`}
                >
                  <X className="size-3" aria-hidden />
                </button>
              </Badge>
            );
          })}
        </div>
      )}
    </div>
  );
}
