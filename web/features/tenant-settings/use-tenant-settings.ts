"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { queryKeys } from "@/lib/api/query-keys";

import type { TenantSetting, TenantSettingChange } from "./types";

const path = (key: string) => `/settings/${encodeURIComponent(key)}`;

/** Los settings que el contribuyente puede ajustar, con su valor efectivo. */
export function useTenantSettings() {
  return useQuery({
    queryKey: queryKeys.tenantSettings.list(),
    queryFn: () => api.get<TenantSetting[]>("/settings"),
  });
}

/** Sobrescribe el valor de un setting del contribuyente. */
export function useUpdateTenantSetting() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { key: string; value: string }) =>
      api.put<TenantSetting>(path(input.key), { value: input.value }),
    onSuccess: (updated) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.tenantSettings.all,
      });
      toast.success(`«${updated.label}» actualizado`);
    },
    onError: (error) => toast.error(tenantSettingErrorMessage(error)),
  });
}

/** Quita el override: vuelve a regir el valor por defecto. */
export function useResetTenantSetting() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { key: string }) =>
      api.delete<TenantSetting>(path(input.key)),
    onSuccess: (updated) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.tenantSettings.all,
      });
      toast.success(`«${updated.label}» restablecido`);
    },
    onError: (error) => toast.error(tenantSettingErrorMessage(error)),
  });
}

/** El historial de cambios de un setting. `enabled` para no pedirlo hasta abrir el dialog. */
export function useTenantSettingHistory(key: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.tenantSettings.history(key),
    queryFn: () => api.get<TenantSettingChange[]>(`${path(key)}/history`),
    enabled,
  });
}

/**
 * El mensaje que se muestra ante un error de mutación. Un valor inválido vuelve
 * como 400 con el error por campo; el resto usa el `title`, que la API ya
 * devuelve en español.
 */
export function tenantSettingErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.isValidation) {
      const first = Object.values(error.fieldErrors).at(0);
      if (first !== undefined) return first;
    }
    return error.message;
  }
  return "No se pudo guardar el cambio.";
}
