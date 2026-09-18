"use client";

import { AuditLogTable } from "@/components/shared/audit-log-table";
import { useTableSearchParams } from "@/components/shared/data-table";

import { useTenantAuditLog } from "./use-tenant-audit-log";

/** El registro de auditoría de un contribuyente puntual, visto por el operador. */
export function AuditLogTab({ tenantId }: { tenantId: string }) {
  const [state, setState] = useTableSearchParams([]);
  const { data, isPending, isFetching, error } = useTenantAuditLog(
    tenantId,
    state,
  );

  return (
    <AuditLogTable
      page={data}
      isPending={isPending}
      isFetching={isFetching}
      error={error}
      state={state}
      onStateChange={setState}
    />
  );
}
