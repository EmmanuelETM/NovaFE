"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { queryKeys } from "@/lib/api/query-keys";
import type { TenantsListState } from "@/features/tenants/use-tenants";

import type {
  Organization,
  OrganizationMember,
  OrganizationPage,
  OrganizationTenantPage,
} from "./types";

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

/** Las organizaciones de la plataforma, paginado (operador, Fase 5). */
export function useOrganizations(state: TenantsListState, enabled = true) {
  return useQuery({
    queryKey: queryKeys.organizations.list({ ...state }),
    queryFn: () =>
      api.get<OrganizationPage>("/organizations", {
        page: state.page,
        pageSize: state.pageSize,
        search: state.search.trim() || undefined,
      }),
    enabled,
  });
}

/** Una organización puntual (operador, Fase 5). */
export function useOrganization(id: string) {
  return useQuery({
    queryKey: queryKeys.organizations.detail(id),
    queryFn: () => api.get<Organization>(`/organizations/${id}`),
    enabled: id !== "",
  });
}

export interface RegisterOrganizationInput {
  name: string;
  slug: string;
  plan: string;
  /** Si viene, da de alta (o reusa) ese correo como `owner` en el mismo paso. */
  ownerEmail?: string;
}

/** Da de alta una organización. Operador. */
export function useRegisterOrganization() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: RegisterOrganizationInput) =>
      api.post<{ id: string }>("/organizations", input),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.all,
      });
      toast.success("Organización creada");
    },
    onError: (error) => toast.error(organizationErrorMessage(error)),
  });
}

/** Suspende la organización — bloquea en cascada a todos sus tenants. Operador. */
export function useSuspendOrganization(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () =>
      api.post<void>(`/organizations/${organizationId}/suspend`),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.detail(organizationId),
      });
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.all,
      });
      toast.success("Organización suspendida");
    },
    onError: (error) => toast.error(organizationErrorMessage(error)),
  });
}

/** Reactiva una organización suspendida. Operador. */
export function useActivateOrganization(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () =>
      api.post<void>(`/organizations/${organizationId}/activate`),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.detail(organizationId),
      });
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.all,
      });
      toast.success("Organización reactivada");
    },
    onError: (error) => toast.error(organizationErrorMessage(error)),
  });
}

/** Corrige el plan de la organización. Sin pasarela de pago todavía — corrección manual del operador. */
export function useUpdateOrganizationPlan(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (plan: string) =>
      api.patch<void>(`/organizations/${organizationId}/plan`, { plan }),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.detail(organizationId),
      });
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.all,
      });
      toast.success("Plan actualizado");
    },
    onError: (error) => toast.error(organizationErrorMessage(error)),
  });
}

/**
 * Los tenants ("proyectos"/RNCs) de la organización. Self-service desde
 * Fase 4: `GET /organizations/{id}/tenants` ya no es operator-only —
 * cualquier miembro de la organización puede verlos.
 */
export function useOrganizationTenants(organizationId: string) {
  return useQuery({
    queryKey: queryKeys.organizations.tenants(organizationId),
    queryFn: () =>
      api.get<OrganizationTenantPage>(
        `/organizations/${organizationId}/tenants`,
        { page: 1, pageSize: 100 },
      ),
  });
}

/** Asocia (o reasocia) un tenant existente a la organización. Operador. */
export function useAssignTenantToOrganization(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (tenantId: string) =>
      api.post<void>(`/organizations/${organizationId}/tenants/${tenantId}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.tenants(organizationId),
      });
      toast.success("Tenant asociado");
    },
    onError: (error) => toast.error(organizationErrorMessage(error)),
  });
}

/** Desasocia un tenant de la organización — vuelve a quedar huérfano. Operador. */
export function useUnassignTenantFromOrganization(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (tenantId: string) =>
      api.delete<void>(`/organizations/${organizationId}/tenants/${tenantId}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organizations.tenants(organizationId),
      });
      toast.success("Tenant quitado de la organización");
    },
    onError: (error) => toast.error(organizationErrorMessage(error)),
  });
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

/** Invita a un miembro por correo — si el correo es nuevo, se da de alta en el mismo paso. */
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
