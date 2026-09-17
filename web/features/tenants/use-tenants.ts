"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { queryKeys } from "@/lib/api/query-keys";

import type { EmitterProfile, Tenant, TenantPage } from "./types";

/** El estado del listado, tal como lo maneja `useTableSearchParams`. */
export interface TenantsListState {
  page: number;
  pageSize: number;
  search: string;
}

/**
 * Contribuyentes, paginado en el servidor. El parámetro de búsqueda de este
 * endpoint es `search` (no `filter`, el nombre genérico de `toApiQuery`), así
 * que la consulta se arma a mano en vez de con ese helper.
 */
export function useTenants(state: TenantsListState) {
  return useQuery({
    queryKey: queryKeys.tenants.list({ ...state }),
    queryFn: () =>
      api.get<TenantPage>("/tenants", {
        page: state.page,
        pageSize: state.pageSize,
        search: state.search.trim() || undefined,
      }),
  });
}

export function useTenant(id: string) {
  return useQuery({
    queryKey: queryKeys.tenants.detail(id),
    queryFn: () => api.get<Tenant>(`/tenants/${id}`),
    enabled: id !== "",
  });
}

export function useEmitterProfile(id: string) {
  return useQuery({
    queryKey: [...queryKeys.tenants.detail(id), "emitter-profile"] as const,
    queryFn: () => api.get<EmitterProfile>(`/tenants/${id}/emitter-profile`),
    enabled: id !== "",
    retry: false,
  });
}

export interface RegisterTenantInput {
  rnc: string;
  legalName: string;
  tradeName?: string;
}

export function useRegisterTenant() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: RegisterTenantInput) =>
      api.post<{ id: string }>("/tenants", input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.tenants.all });
    },
    onError: (error) => toast.error(tenantErrorMessage(error)),
  });
}

export interface SetEmitterProfileInput {
  tenantId: string;
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
    mutationFn: ({ tenantId, ...body }: SetEmitterProfileInput) =>
      api.put<EmitterProfile>(`/tenants/${tenantId}/emitter-profile`, body),
    onSuccess: (_data, { tenantId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.tenants.detail(tenantId),
      });
      toast.success("Perfil de emisor guardado");
    },
    onError: (error) => toast.error(tenantErrorMessage(error)),
  });
}

/** El mensaje ante un error de mutación. Un 400 trae el detalle en `fieldErrors`. */
export function tenantErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.isValidation) {
      const first = Object.values(error.fieldErrors).at(0);
      if (first !== undefined) return first;
    }
    return error.message;
  }
  return "No se pudo completar la acción.";
}
