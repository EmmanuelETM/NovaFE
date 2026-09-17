"use client";

import { useQuery } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import type { DeadWebhookDeliveryPage } from "./types";

/**
 * Entregas de webhook `dead` de toda la plataforma (operador), paginado. Vive
 * en `webhook_deliveries`, tabla de sistema sin RLS — cruza tenants a
 * propósito. Mismo `refetchInterval` que `useOpsStatus`: es la misma
 * pantalla en vivo.
 */
export function useDeadDeliveries(page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.ops.deadDeliveries({ page, pageSize }),
    queryFn: () =>
      api.get<DeadWebhookDeliveryPage>("/ops/dead-deliveries", {
        page,
        pageSize,
      }),
    refetchInterval: 15_000,
  });
}
