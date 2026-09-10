"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { queryKeys } from "@/lib/api/query-keys";

import type { PlatformUser } from "./types";

/** Ámbito de una acción sobre un usuario: un contribuyente, o `null` = operador del SaaS. */
export type UserScope = { userId: string; tenantId: string | null };

const userPath = ({ userId, tenantId }: UserScope, suffix = "") =>
  tenantId
    ? `/tenants/${tenantId}/users/${userId}${suffix}`
    : `/operator-users/${userId}${suffix}`;

// --- lecturas ---------------------------------------------------------------

/** Operadores del SaaS. */
export function useOperators() {
  return useQuery({
    queryKey: queryKeys.platformUsers.operators(),
    queryFn: () => api.get<PlatformUser[]>("/operator-users"),
  });
}

/** Empleados de un contribuyente. Inactiva hasta que hay un contribuyente elegido. */
export function useTenantUsers(tenantId: string | null) {
  return useQuery({
    queryKey: queryKeys.platformUsers.byTenant(tenantId ?? ""),
    queryFn: () => api.get<PlatformUser[]>(`/tenants/${tenantId}/users`),
    enabled: tenantId !== null,
  });
}

// --- escrituras ------------------------------------------------------------

function useUserMutation<TVars>(
  fn: (vars: TVars) => Promise<unknown>,
  keyFor: (vars: TVars) => readonly unknown[],
  success: string,
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: fn,
    onSuccess: (_data, vars) => {
      void queryClient.invalidateQueries({ queryKey: keyFor(vars) });
      toast.success(success);
    },
    onError: (error) => toast.error(userErrorMessage(error)),
  });
}

export function useProvisionOperator() {
  return useUserMutation(
    (input: { email: string }) =>
      api.post<PlatformUser>("/operator-users", input),
    () => queryKeys.platformUsers.operators(),
    "Operador dado de alta",
  );
}

export function useProvisionTenantUser() {
  return useUserMutation(
    (input: { tenantId: string; email: string; role: string }) =>
      api.post<PlatformUser>(`/tenants/${input.tenantId}/users`, {
        email: input.email,
        role: input.role,
      }),
    (input) => queryKeys.platformUsers.byTenant(input.tenantId),
    "Usuario dado de alta",
  );
}

export function useChangeUserRole() {
  return useUserMutation(
    (input: { tenantId: string; userId: string; role: string }) =>
      api.patch<PlatformUser>(
        `/tenants/${input.tenantId}/users/${input.userId}`,
        {
          role: input.role,
        },
      ),
    (input) => queryKeys.platformUsers.byTenant(input.tenantId),
    "Rol actualizado",
  );
}

export function useRevokeUser() {
  return useUserMutation(
    (scope: UserScope) => api.delete<void>(userPath(scope)),
    scopeKey,
    "Acceso revocado",
  );
}

export function useReinstateUser() {
  return useUserMutation(
    (scope: UserScope) => api.post<void>(userPath(scope, "/reinstate")),
    scopeKey,
    "Acceso reactivado",
  );
}

const scopeKey = (scope: UserScope) =>
  scope.tenantId
    ? queryKeys.platformUsers.byTenant(scope.tenantId)
    : queryKeys.platformUsers.operators();

/** El mensaje ante un error de mutación. Un 400 trae el detalle en `fieldErrors`. */
export function userErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.isValidation) {
      const first = Object.values(error.fieldErrors).at(0);
      if (first !== undefined) return first;
    }
    return error.message;
  }
  return "No se pudo completar la acción.";
}
