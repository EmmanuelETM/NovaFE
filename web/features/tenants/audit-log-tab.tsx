"use client";

import {
  DataTable,
  createAppColumnHelper,
  useTableSearchParams,
  type PagedResult,
} from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { roleLabel } from "@/features/auth/roles";
import type {
  AuditLogEntry,
  AuditLogPage,
} from "@/features/tenant-config/types";
import { formatDateTime } from "@/lib/format";

import { useTenantAuditLog } from "./use-tenant-audit-log";

const ch = createAppColumnHelper<AuditLogEntry>();

const columns = ch.columns([
  ch.accessor("occurredAt", {
    header: "Fecha",
    cell: (cell) => formatDateTime(cell.getValue()),
  }),
  ch.display({
    id: "actor",
    header: "Actor",
    cell: (cell) => {
      const { actor, actorRole, impersonatedBy } = cell.row.original;
      return (
        <div className="flex flex-col">
          <span className="truncate font-mono text-xs">{actor}</span>
          {actorRole && (
            <span className="text-muted-foreground text-xs">
              {roleLabel(actorRole)}
            </span>
          )}
          {impersonatedBy && (
            <span className="text-xs text-amber-700 dark:text-amber-400">
              impersonado por {impersonatedBy}
            </span>
          )}
        </div>
      );
    },
  }),
  ch.display({
    id: "peticion",
    header: "Petición",
    cell: (cell) => (
      <span className="font-mono text-xs">
        {cell.row.original.httpMethod} {cell.row.original.path}
      </span>
    ),
  }),
  ch.display({
    id: "resultado",
    header: "Resultado",
    cell: (cell) => (
      <Badge variant={cell.row.original.succeeded ? "outline" : "destructive"}>
        {cell.row.original.statusCode}
      </Badge>
    ),
  }),
]);

function toPage(
  page: AuditLogPage | undefined,
): PagedResult<AuditLogEntry> | undefined {
  if (!page) return undefined;

  return {
    items: page.items,
    totalCount: Number(page.totalCount),
    page: Number(page.page),
    pageSize: Number(page.pageSize),
    totalPages: Number(page.totalPages ?? 1),
    hasNextPage: page.hasNextPage ?? false,
    hasPreviousPage: page.hasPreviousPage ?? false,
  };
}

/** El registro de auditoría de un contribuyente puntual, visto por el operador. */
export function AuditLogTab({ tenantId }: { tenantId: string }) {
  const [state, setState] = useTableSearchParams([]);
  const { data, isPending, isFetching, error } = useTenantAuditLog(
    tenantId,
    state,
  );

  return (
    <DataTable
      columns={columns}
      page={toPage(data)}
      isPending={isPending}
      isFetching={isFetching}
      error={error}
      state={state}
      onStateChange={setState}
      searchable={false}
      emptyState={{ title: "Sin actividad registrada todavía." }}
      getRowId={(entry) => entry.id}
    />
  );
}
