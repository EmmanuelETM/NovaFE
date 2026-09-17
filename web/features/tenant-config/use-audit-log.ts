"use client";

import { useQuery } from "@tanstack/react-query";

import type { DataTableSearchState } from "@/components/shared/data-table";
import { useTenantId } from "@/features/auth/use-tenant-id";
import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import type { AuditLogPage } from "./types";

/**
 * El registro de auditoría (RF-14.4) del tenant activo, paginado en el
 * servidor. El endpoint no acepta búsqueda ni filtros — solo `page`/`pageSize`.
 */
export function useAuditLog(
  state: Pick<DataTableSearchState, "page" | "pageSize">,
) {
  const tenantId = useTenantId();

  return useQuery({
    queryKey: queryKeys.myAuditLog.list(tenantId, { ...state }),
    queryFn: () =>
      api.get<AuditLogPage>(
        "/audit-log",
        { page: state.page, pageSize: state.pageSize },
        tenantId,
      ),
  });
}
