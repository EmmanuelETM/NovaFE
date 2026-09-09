"use client";

import { useCallback, useMemo } from "react";
import {
  parseAsInteger,
  parseAsString,
  useQueryStates,
  type UseQueryStatesKeysMap,
} from "nuqs";

import type { DataTableSearchState } from "./types";

export const DEFAULT_PAGE_SIZE = 20;

/** Los que la API acepta. Pedir más se recorta en el servidor, así que no se ofrece. */
export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;

/**
 * El estado del listado, sincronizado con la URL.
 *
 * Devuelve el estado y una función que aplica cambios parciales. **Cualquier cambio que no
 * sea de página vuelve a la página 1**, que es lo que uno espera: filtrar estando en la
 * página 7 y quedarse en la 7 de un resultado de dos páginas muestra una tabla vacía y
 * parece un error.
 *
 * `history: "replace"` a propósito: teclear en el buscador no debe llenar el historial de
 * una entrada por letra. Cambiar de página sí usa `push`, para que «atrás» funcione.
 */
export function useTableSearchParams(filterKeys: readonly string[] = []) {
  // Las dependencias de un memo tienen que ser expresiones simples —lo exige el lint de
  // React Compiler— así que la identidad del arreglo se resuelve en una variable antes.
  const filterKeysId = filterKeys.join(",");

  // La forma del mapa depende de los filtros de cada pantalla, así que se construye aquí.
  const keys = useMemo(() => {
    const map: UseQueryStatesKeysMap = {
      page: parseAsInteger.withDefault(1),
      pageSize: parseAsInteger.withDefault(DEFAULT_PAGE_SIZE),
      search: parseAsString.withDefault(""),
      sort: parseAsString,
    };

    for (const key of filterKeys) map[key] = parseAsString;

    return map;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKeysId]);

  const [raw, setRaw] = useQueryStates(keys, {
    history: "replace",
    clearOnDefault: true,
  });

  const state: DataTableSearchState = useMemo(() => {
    const filters: Record<string, string | null> = {};

    for (const key of filterKeys) {
      const value = raw[key];
      filters[key] = typeof value === "string" ? value : null;
    }

    return {
      page: typeof raw.page === "number" ? raw.page : 1,
      pageSize:
        typeof raw.pageSize === "number" ? raw.pageSize : DEFAULT_PAGE_SIZE,
      search: typeof raw.search === "string" ? raw.search : "",
      sort: typeof raw.sort === "string" ? raw.sort : null,
      filters,
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [raw, filterKeysId]);

  const setState = useCallback(
    (patch: Partial<DataTableSearchState>) => {
      const { filters, ...rest } = patch;

      // Todo lo que cambie el conjunto de resultados vuelve a la primera pagina.
      const resetsPage =
        rest.search !== undefined ||
        rest.sort !== undefined ||
        rest.pageSize !== undefined ||
        filters !== undefined;

      void setRaw(
        {
          ...rest,
          ...(filters ?? {}),
          ...(resetsPage && rest.page === undefined ? { page: 1 } : {}),
        },
        { history: rest.page !== undefined ? "push" : "replace" },
      );
    },
    [setRaw],
  );

  return [state, setState] as const;
}

/**
 * El estado del listado traducido a parámetros de consulta de la API.
 *
 * ⚠️ **Aquí vive la conversión de página.** La API cuenta desde 1 y TanStack Table desde 0;
 * hacerla en cada pantalla es cómo aparecen los errores de una página de corrimiento.
 */
export function toApiQuery(
  state: DataTableSearchState,
): Record<string, string | number | undefined> {
  const query: Record<string, string | number | undefined> = {
    page: state.page,
    pageSize: state.pageSize,
  };

  if (state.search.trim() !== "") query.filter = state.search.trim();

  // El nombre del campo es el que la API declara, y coincide con el `id` de la columna:
  // por eso el estado de orden de la tabla se puede mandar tal cual.
  if (state.sort) query.sort = state.sort;

  for (const [key, value] of Object.entries(state.filters)) {
    if (value !== null && value !== "") query[key] = value;
  }

  return query;
}
