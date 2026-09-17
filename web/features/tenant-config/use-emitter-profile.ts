"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import type { EmitterProfile } from "@/features/tenants/types";

import { tenantConfigErrorMessage } from "./use-certificates";

/** El perfil fiscal del emisor del contribuyente actual (self-service). */
export function useEmitterProfile() {
  return useQuery({
    queryKey: queryKeys.myEmitterProfile,
    queryFn: () => api.get<EmitterProfile>("/emitter-profile"),
    retry: false,
  });
}

export interface SetEmitterProfileInput {
  address: string;
  municipality?: string | null;
  province?: string | null;
  phones?: string[] | null;
  email?: string | null;
  economicActivity?: string | null;
  defaultEnvironment: string;
}

export function useSetEmitterProfile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: SetEmitterProfileInput) =>
      api.put<EmitterProfile>("/emitter-profile", body),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.myEmitterProfile,
      });
      toast.success("Perfil de emisor guardado");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}
