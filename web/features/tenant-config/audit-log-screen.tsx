"use client";

import {
  DataTable,
  createAppColumnHelper,
  useTableSearchParams,
  type PagedResult,
} from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { roleLabel } from "@/features/auth/roles";
import { formatDateTime } from "@/lib/format";

import { useAuditLog } from "./use-audit-log";
import type { AuditLogEntry, AuditLogPage } from "./types";

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
      const { actor, actorRole } = cell.row.original;
      return (
        <div className="flex flex-col">
          <span className="truncate font-mono text-xs">{actor}</span>
          {actorRole && (
            <span className="text-muted-foreground text-xs">
              {roleLabel(actorRole)}
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
  ch.accessor("durationMs", {
    header: "Duración",
    meta: { align: "right" },
    cell: (cell) => {
      const value = cell.getValue();
      return value === null ? "—" : `${value} ms`;
    },
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

/** El registro de auditoría del tenant activo: quién hizo qué, y cuándo. */
export function AuditLogScreen() {
  const [state, setState] = useTableSearchParams([]);
  const { data, isPending, isFetching, error } = useAuditLog(state);

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
