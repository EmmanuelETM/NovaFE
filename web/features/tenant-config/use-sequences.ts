"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import { tenantConfigErrorMessage } from "./use-certificates";
import type { NcfSequence } from "./types";

/** Los rangos de e-NCF del contribuyente actual (self-service, sin `tenantId`). */
export function useSequences() {
  return useQuery({
    queryKey: queryKeys.mySequences.list(),
    queryFn: () => api.get<NcfSequence[]>("/sequences"),
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
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: RegisterSequenceInput) =>
      api.post<{ id: string }>("/sequences", body),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.mySequences.all,
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
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => api.post<void>(`/sequences/${id}/deactivate`),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.mySequences.all,
      });
      toast.success("Rango desactivado");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}
