"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import type {
  WebhookDeliveryPage,
  WebhookEndpoint,
} from "@/features/tenant-config/types";
import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import { tenantErrorMessage } from "./use-tenants";

/** Los endpoints de webhook de un contribuyente, cargados por el operador. */
export function useTenantWebhooks(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.tenants.webhooks(tenantId),
    queryFn: () => api.get<WebhookEndpoint[]>(`/tenants/${tenantId}/webhooks`),
    enabled: tenantId !== "",
  });
}

/** El log de entregas de un endpoint, cargado por el operador, paginado en el servidor. */
export function useTenantWebhookDeliveries(
  tenantId: string,
  endpointId: string,
  page: number,
  pageSize: number,
) {
  return useQuery({
    queryKey: [
      ...queryKeys.tenants.webhookDeliveries(tenantId, endpointId),
      { page, pageSize },
    ] as const,
    queryFn: () =>
      api.get<WebhookDeliveryPage>(
        `/tenants/${tenantId}/webhooks/${endpointId}/deliveries`,
        { page, pageSize },
      ),
    enabled: tenantId !== "" && endpointId !== "",
  });
}

/** Reintenta manualmente una entrega `dead`, como operador. */
export function useRetryTenantWebhookDelivery(
  tenantId: string,
  endpointId: string,
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (deliveryId: string) =>
      api.post<void>(
        `/tenants/${tenantId}/webhooks/${endpointId}/deliveries/${deliveryId}/retry`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.tenants.webhookDeliveries(tenantId, endpointId),
      });
      toast.success("Entrega reencolada");
    },
    onError: (error) => toast.error(tenantErrorMessage(error)),
  });
}
