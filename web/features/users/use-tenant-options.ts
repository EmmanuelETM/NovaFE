"use client";

import { useQuery } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { queryKeys } from "@/lib/api/query-keys";

import type { TenantSummary } from "./types";

type TenantPage = components["schemas"]["PagedResultOfTenantSummaryDto"];

/**
 * Los contribuyentes para el selector. Trae los primeros 100 y el combobox filtra
 * en el cliente — suficiente al arranque. Si crecen, pasar a búsqueda server-side
 * con el parámetro `?search=` del endpoint.
 */
export function useTenantOptions() {
  return useQuery({
    queryKey: queryKeys.tenants.options(),
    queryFn: async () => {
      const page = await api.get<TenantPage>("/tenants", { pageSize: 100 });
      return (page.items ?? []) as TenantSummary[];
    },
    staleTime: 5 * 60_000,
  });
}
