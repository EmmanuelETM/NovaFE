"use client";

import { AuditLogTable } from "@/components/shared/audit-log-table";
import { useTableSearchParams } from "@/components/shared/data-table";

import { useOrganizationAuditLog } from "./use-organization-audit-log";

/** Lo que tocó a esta organización (plan, estado, miembros, tenants). */
export function OrganizationAuditLogTab({
  organizationId,
}: {
  organizationId: string;
}) {
  const [state, setState] = useTableSearchParams([]);
  const { data, isPending, isFetching, error } = useOrganizationAuditLog(
    organizationId,
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
