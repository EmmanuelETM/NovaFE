"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import { tenantErrorMessage } from "./use-tenants";
import type { ApiKey, ApiKeyCreated } from "./types";

/** Las API keys de un contribuyente (sin los tokens). */
export function useTenantApiKeys(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.tenants.apiKeys(tenantId),
    queryFn: () => api.get<ApiKey[]>(`/tenants/${tenantId}/api-keys`),
    enabled: tenantId !== "",
  });
}

export interface CreateApiKeyInput {
  tenantId: string;
  label?: string | null;
  environment?: string | null;
  role: string;
  expiresAt?: string | null;
}

/**
 * Acuña una API key. Solo tiene éxito si el contribuyente ya tiene certificado
 * activo y rango de e-NCF para ese ambiente — la regla que resuelven las
 * pestañas de Certificados y Secuencias antes que esta.
 */
export function useCreateApiKey() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ tenantId, ...body }: CreateApiKeyInput) =>
      api.post<ApiKeyCreated>(`/tenants/${tenantId}/api-keys`, body),
    onSuccess: (_data, { tenantId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.tenants.apiKeys(tenantId),
      });
    },
    onError: (error) => toast.error(tenantErrorMessage(error)),
  });
}

export function useRevokeApiKey() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ tenantId, keyId }: { tenantId: string; keyId: string }) =>
      api.delete<void>(`/tenants/${tenantId}/api-keys/${keyId}`),
    onSuccess: (_data, { tenantId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.tenants.apiKeys(tenantId),
      });
      toast.success("API key revocada");
    },
    onError: (error) => toast.error(tenantErrorMessage(error)),
  });
}
