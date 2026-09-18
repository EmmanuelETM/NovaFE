"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import type { DataTableSearchState } from "@/components/shared/data-table";
import { useTenantId } from "@/features/auth/use-tenant-id";
import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { queryKeys } from "@/lib/api/query-keys";

import type { Ecf, EcfPage, FiscalSummary } from "./types";

/**
 * Comprobantes emitidos del contribuyente actual, paginado en el servidor.
 * El endpoint no acepta `sort` — siempre viene en orden de emisión más
 * reciente. El filtro `type` necesita ser numérico, así que la consulta se
 * arma a mano en vez de con `toApiQuery` (misma razón que documenta
 * `use-tenants.ts` para `search` vs. `filter`). `from`/`to` son el mismo
 * rango de fechas que ya filtra el resumen fiscal de arriba de la pantalla
 * — una sola fuente de verdad (`EcfScreen`), no dos filtros de fecha.
 */
export function useEcfList(state: DataTableSearchState) {
  const tenantId = useTenantId();
  const type = state.filters.type;
  const status = state.filters.status;

  return useQuery({
    queryKey: queryKeys.ecf.list(tenantId, { ...state }),
    queryFn: () =>
      api.get<EcfPage>(
        "/ecf",
        {
          page: state.page,
          pageSize: state.pageSize,
          search: state.search.trim() || undefined,
          type: type ? Number(type) : undefined,
          status: status ?? undefined,
          from: state.filters.from ?? undefined,
          to: state.filters.to ?? undefined,
        },
        tenantId,
      ),
  });
}

/**
 * Resumen fiscal del contribuyente actual entre `from` y `to` (días locales
 * `YYYY-MM-DD`, inclusive) — el mismo formato que produce `DateRangePicker`.
 * El endpoint toma `DateOnly`, no un instante: no pasa por `dayRange()`.
 */
export function useFiscalSummary(from: string, to: string) {
  const tenantId = useTenantId();

  return useQuery({
    queryKey: queryKeys.finance.summary(tenantId, from, to),
    queryFn: () =>
      api.get<FiscalSummary>("/finance/summary", { from, to }, tenantId),
  });
}

export function useEcf(id: string) {
  const tenantId = useTenantId();

  return useQuery({
    queryKey: queryKeys.ecf.detail(tenantId, id),
    queryFn: () => api.get<Ecf>(`/ecf/${id}`, undefined, tenantId),
    enabled: id !== "",
  });
}

/** Reencola el envío a la DGII de un comprobante `failed`/`review`. */
export function useRetryEcf() {
  const tenantId = useTenantId();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) =>
      api.post<Ecf>(`/ecf/${id}/retry`, undefined, tenantId),
    onSuccess: (_data, id) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.ecf.detail(tenantId, id),
      });
      void queryClient.invalidateQueries({
        queryKey: queryKeys.ecf.all(tenantId),
      });
      toast.success("Comprobante reencolado para un nuevo intento de envío");
    },
    onError: (error) => toast.error(ecfErrorMessage(error)),
  });
}

/** El mensaje ante un error de mutación. Un 400 trae el detalle en `fieldErrors`. */
export function ecfErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.isValidation) {
      const first = Object.values(error.fieldErrors).at(0);
      if (first !== undefined) return first;
    }
    return error.message;
  }
  return "No se pudo completar la acción.";
}
