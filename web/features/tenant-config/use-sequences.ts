"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { useTenantId } from "@/features/auth/use-tenant-id";
import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import { tenantConfigErrorMessage } from "./use-certificates";
import type { NcfSequence } from "./types";

/** Los rangos de e-NCF del tenant activo (self-service). */
export function useSequences() {
  const tenantId = useTenantId();

  return useQuery({
    queryKey: queryKeys.mySequences.list(tenantId),
    queryFn: () => api.get<NcfSequence[]>("/sequences", undefined, tenantId),
  });
}

export interface RegisterSequenceInput {
  environment: string;
  type: number;
  series: string;
  rangeFrom: number;
  rangeTo: number;
  authorizedOn?: string | null;
}

export function useRegisterSequence() {
  const tenantId = useTenantId();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: RegisterSequenceInput) =>
      api.post<{ id: string }>("/sequences", body, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.mySequences.all(tenantId),
      });
      toast.success("Rango de e-NCF registrado");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}

/**
 * Desactiva un rango — típicamente uno agotado, para poder registrar uno
 * nuevo con la misma serie (no puede haber dos rangos activos de la misma
 * serie/tipo/ambiente a la vez).
 */
export function useDeactivateSequence() {
  const tenantId = useTenantId();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) =>
      api.post<void>(`/sequences/${id}/deactivate`, undefined, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.mySequences.all(tenantId),
      });
      toast.success("Rango desactivado");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}
