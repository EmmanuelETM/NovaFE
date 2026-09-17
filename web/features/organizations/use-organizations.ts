"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { queryKeys } from "@/lib/api/query-keys";

import type { OrganizationMember } from "./types";

/** El mensaje ante un error de mutación. Un 400 trae el detalle en `fieldErrors`. */
export function organizationErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.isValidation) {
      const first = Object.values(error.fieldErrors).at(0);
      if (first !== undefined) return first;
    }
    return error.message;
  }
  return "No se pudo completar la acción.";
}

/** Los miembros de la organización. Self-service: cualquier miembro puede verlos. */
export function useOrganizationMembers(organizationId: string) {
  return useQuery({
    queryKey: queryKeys.organizations.members(organizationId),
    queryFn: () =>
      api.get<OrganizationMember[]>(`/organizations/${organizationId}/members`),
  });
}

export interface AddOrganizationMemberInput {
  email: string;
  role: string;
}

/** Invita a un miembro por correo. Requiere que ya exista como usuario de la plataforma. */
export function useAddOrganizationMember(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: AddOrganizationMemberInput) =>
      api.post<OrganizationMember>(
        `/organizations/${organizationId}/members`,
        body,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.members(organizationId),
      });
      toast.success("Miembro agregado");
    },
    onError: (error) => toast.error(organizationErrorMessage(error)),
  });
}

/** Cambia el rol de un miembro. Solo `owner`/`admin` de esta organización. */
export function useChangeOrganizationMemberRole(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: string }) =>
      api.patch<OrganizationMember>(
        `/organizations/${organizationId}/members/${userId}`,
        { role },
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.members(organizationId),
      });
      toast.success("Rol actualizado");
    },
    onError: (error) => toast.error(organizationErrorMessage(error)),
  });
}

/** Quita a un miembro de la organización. No se puede quitar al último `owner`. */
export function useRemoveOrganizationMember(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (userId: string) =>
      api.delete<void>(`/organizations/${organizationId}/members/${userId}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.members(organizationId),
      });
      toast.success("Miembro quitado");
    },
    onError: (error) => toast.error(organizationErrorMessage(error)),
  });
}
