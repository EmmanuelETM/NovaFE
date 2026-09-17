import type { DataTableSearchState, PagedResult } from "./types";

/**
 * Estado fijo para una tabla cuyo endpoint **no pagina** — devuelve el
 * arreglo completo (catálogos chicos por contribuyente: certificados,
 * secuencias, webhooks, usuarios, API keys). `DataTable` igual pide un
 * `PagedResult` y un estado de listado; esto se los da sin que la pantalla
 * tenga que inventarlos.
 */
export const STATIC_TABLE_STATE: DataTableSearchState = {
  page: 1,
  pageSize: 100,
  search: "",
  sort: null,
  filters: {},
};

/** Envuelve un arreglo completo en la única "página" que `DataTable` espera. */
export function toStaticPage<T>(
  items: T[] | undefined,
): PagedResult<T> | undefined {
  if (!items) return undefined;

  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: Math.max(items.length, 1),
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  };
}
