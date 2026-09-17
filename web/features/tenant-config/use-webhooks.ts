"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import { tenantConfigErrorMessage } from "./use-certificates";
import type {
  WebhookEndpoint,
  WebhookEndpointCreated,
  WebhookPingResult,
  WebhookSecret,
} from "./types";

/** Los endpoints de webhook del contribuyente actual. */
export function useWebhooks() {
  return useQuery({
    queryKey: queryKeys.webhooks.list(),
    queryFn: () => api.get<WebhookEndpoint[]>("/webhooks"),
  });
}

export interface CreateWebhookInput {
  url: string;
  events: string[];
  description?: string;
}

export function useCreateWebhook() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateWebhookInput) =>
      api.post<WebhookEndpointCreated>("/webhooks", body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.webhooks.all });
      toast.success("Webhook registrado");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}

export interface UpdateWebhookInput {
  id: string;
  url?: string;
  events?: string[];
  enabled?: boolean;
  description?: string;
}

export function useUpdateWebhook() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...body }: UpdateWebhookInput) =>
      api.patch<WebhookEndpoint>(`/webhooks/${id}`, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.webhooks.all });
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}

export function useRotateWebhookSecret() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) =>
      api.post<WebhookSecret>(`/webhooks/${id}/rotate-secret`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.webhooks.all });
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}

/** No invalida nada: el resultado se avisa por toast, no cambia el estado del endpoint. */
export function usePingWebhook() {
  return useMutation({
    mutationFn: (id: string) =>
      api.post<WebhookPingResult>(`/webhooks/${id}/ping`),
    onSuccess: (result) => {
      if (result.delivered) {
        toast.success(`Entregado (${result.statusCode ?? "sin código"})`);
      } else {
        toast.error(
          `No se pudo entregar${result.error ? `: ${result.error}` : ""}`,
        );
      }
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}

export function useDeleteWebhook() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/webhooks/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.webhooks.all });
      toast.success("Webhook eliminado");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}
