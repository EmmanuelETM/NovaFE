"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { useTenantId } from "@/features/auth/use-tenant-id";
import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import { tenantConfigErrorMessage } from "./use-certificates";
import type { ApiKey, ApiKeyCreated } from "./types";

/**
 * Las API keys del tenant activo (self-service, `TenantConfig`).
 *
 * **No confundir** con `features/tenants/use-tenant-api-keys.ts`: ese es el
 * camino de **operador** (`/tenants/{id}/api-keys`), usado en Nemus. Este
 * pega directo a `/api-keys`, resuelto por el tenant activo vía
 * `X-Active-Tenant-Id` — el mismo patrón que certificados/secuencias/webhooks.
 */
export function useApiKeys() {
  const tenantId = useTenantId();

  return useQuery({
    queryKey: queryKeys.myApiKeys.list(tenantId),
    queryFn: () => api.get<ApiKey[]>("/api-keys", undefined, tenantId),
  });
}

export interface CreateApiKeyInput {
  label?: string | null;
  environment?: string | null;
  role: string;
  expiresAt?: string | null;
}

/**
 * Acuña una API key. Solo tiene éxito si el contribuyente ya tiene
 * certificado activo y rango de e-NCF para ese ambiente.
 */
export function useCreateApiKey() {
  const tenantId = useTenantId();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateApiKeyInput) =>
      api.post<ApiKeyCreated>("/api-keys", body, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.myApiKeys.all(tenantId),
      });
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}

export function useRevokeApiKey() {
  const tenantId = useTenantId();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/api-keys/${id}`, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.myApiKeys.all(tenantId),
      });
      toast.success("API key revocada");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}
