"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { queryKeys } from "@/lib/api/query-keys";

import type { PlatformSetting } from "./types";

const path = (key: string) => `/platform-settings/${encodeURIComponent(key)}`;

/** Todos los settings de plataforma con su valor efectivo. */
export function usePlatformSettings() {
  return useQuery({
    queryKey: queryKeys.platformSettings.list(),
    queryFn: () => api.get<PlatformSetting[]>("/platform-settings"),
  });
}

/** Sobrescribe el valor de un setting. */
export function useUpdatePlatformSetting() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { key: string; value: string }) =>
      api.put<PlatformSetting>(path(input.key), { value: input.value }),
    onSuccess: (updated) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.platformSettings.all,
      });
      toast.success(`«${updated.label}» actualizado`);
    },
    onError: (error) => toast.error(settingErrorMessage(error)),
  });
}

/** Quita el override: vuelve a regir el valor por defecto. */
export function useResetPlatformSetting() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (key: string) => api.delete<PlatformSetting>(path(key)),
    onSuccess: (updated) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.platformSettings.all,
      });
      toast.success(`«${updated.label}» restablecido`);
    },
    onError: (error) => toast.error(settingErrorMessage(error)),
  });
}

/**
 * El mensaje que se muestra ante un error de mutación.
 *
 * Un valor inválido vuelve como 400 con el error por campo (clave `Setting.<key>`);
 * ahí el `title` del ProblemDetails es genérico, así que se toma el mensaje del
 * campo. Todo lo demás usa el `title`, que la API ya devuelve en español.
 */
export function settingErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.isValidation) {
      const first = Object.values(error.fieldErrors).at(0);
      if (first !== undefined) return first;
    }
    return error.message;
  }
  return "No se pudo guardar el cambio.";
}
