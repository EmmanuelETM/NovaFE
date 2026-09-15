"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import { tenantErrorMessage } from "./use-tenants";
import type { NcfSequence } from "./types";

/** Los rangos de e-NCF de un contribuyente, registrados por el operador. */
export function useTenantSequences(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.tenants.sequences(tenantId),
    queryFn: () => api.get<NcfSequence[]>(`/tenants/${tenantId}/sequences`),
    enabled: tenantId !== "",
  });
}

export interface RegisterSequenceInput {
  tenantId: string;
  environment: string;
  type: number;
  series: string;
  rangeFrom: number;
  rangeTo: number;
  authorizedOn?: string | null;
}

export function useRegisterSequence() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ tenantId, ...body }: RegisterSequenceInput) =>
      api.post<{ id: string }>(`/tenants/${tenantId}/sequences`, body),
    onSuccess: (_data, { tenantId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.tenants.sequences(tenantId),
      });
      toast.success("Rango de e-NCF registrado");
    },
    onError: (error) => toast.error(tenantErrorMessage(error)),
  });
}
