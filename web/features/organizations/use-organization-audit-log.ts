"use client";

import { useQuery } from "@tanstack/react-query";

import type { DataTableSearchState } from "@/components/shared/data-table";
import type { AuditLogPage } from "@/features/tenant-config/types";
import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

/** Lo que tocó a esta organización (plan, estado, miembros, tenants). Operador. */
export function useOrganizationAuditLog(
  organizationId: string,
  state: Pick<DataTableSearchState, "page" | "pageSize">,
) {
  return useQuery({
    queryKey: queryKeys.organizations.auditLog(organizationId, { ...state }),
    queryFn: () =>
      api.get<AuditLogPage>(`/organizations/${organizationId}/audit-log`, {
        page: state.page,
        pageSize: state.pageSize,
      }),
    enabled: organizationId !== "",
  });
}
