"use client";

import { useQuery } from "@tanstack/react-query";

import { useTenantId } from "@/features/auth/use-tenant-id";
import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import type { FiscalSummary } from "./types";

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
