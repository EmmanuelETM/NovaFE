"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { useTenantId } from "@/features/auth/use-tenant-id";
import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import { tenantConfigErrorMessage } from "./use-certificates";
import type { WebhookDeliveryPage } from "./types";

/** El log de entregas de un endpoint de webhook, paginado en el servidor. */
export function useWebhookDeliveries(
  endpointId: string,
  page: number,
  pageSize: number,
) {
  const tenantId = useTenantId();

  return useQuery({
    queryKey: queryKeys.webhookDeliveries.list(tenantId, endpointId, {
      page,
      pageSize,
    }),
    queryFn: () =>
      api.get<WebhookDeliveryPage>(
        `/webhooks/${endpointId}/deliveries`,
        { page, pageSize },
        tenantId,
      ),
    enabled: endpointId !== "",
  });
}

/** Reintenta manualmente una entrega `dead` — vuelve a `pending`, lista de inmediato. */
export function useRetryWebhookDelivery(endpointId: string) {
  const tenantId = useTenantId();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (deliveryId: string) =>
      api.post<void>(
        `/webhooks/${endpointId}/deliveries/${deliveryId}/retry`,
        undefined,
        tenantId,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.webhookDeliveries.all(tenantId, endpointId),
      });
      toast.success("Entrega reencolada");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}
