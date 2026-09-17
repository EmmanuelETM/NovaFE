"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { useTenantId } from "@/features/auth/use-tenant-id";
import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import type { EmitterProfile } from "@/features/tenants/types";

import { tenantConfigErrorMessage } from "./use-certificates";

/** El perfil fiscal del emisor del tenant activo (self-service). */
export function useEmitterProfile() {
  const tenantId = useTenantId();

  return useQuery({
    queryKey: queryKeys.myEmitterProfile(tenantId),
    queryFn: () =>
      api.get<EmitterProfile>("/emitter-profile", undefined, tenantId),
    retry: false,
  });
}

/**
 * Sin `defaultEnvironment` a propósito: pasar de un ambiente a otro exige
 * certificado y rango de secuencia ya autorizados ahí, algo que solo
 * confirma el operador — self-service no lo puede tocar (ver el comentario
 * de `UpdateEmitterProfileCommand` en el backend).
 */
export interface UpdateEmitterProfileInput {
  address: string;
  municipality?: string | null;
  province?: string | null;
  phones?: string[] | null;
  email?: string | null;
  economicActivity?: string | null;
}

export function useSetEmitterProfile() {
  const tenantId = useTenantId();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: UpdateEmitterProfileInput) =>
      api.put<EmitterProfile>("/emitter-profile", body, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.myEmitterProfile(tenantId),
      });
      toast.success("Perfil de emisor guardado");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}
