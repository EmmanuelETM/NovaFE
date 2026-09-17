"use client";

import { useQuery } from "@tanstack/react-query";

import type { DataTableSearchState } from "@/components/shared/data-table";
import type { AuditLogPage } from "@/features/tenant-config/types";
import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

/** El registro de auditoría de un contribuyente, cargado por el operador. */
export function useTenantAuditLog(
  tenantId: string,
  state: Pick<DataTableSearchState, "page" | "pageSize">,
) {
  return useQuery({
    queryKey: queryKeys.tenants.auditLog(tenantId, { ...state }),
    queryFn: () =>
      api.get<AuditLogPage>(`/tenants/${tenantId}/audit-log`, {
        page: state.page,
        pageSize: state.pageSize,
      }),
    enabled: tenantId !== "",
  });
}
