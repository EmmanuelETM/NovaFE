"use client";

import {
  DataTable,
  createAppColumnHelper,
  type DataTableSearchState,
  type PagedResult,
} from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { roleLabel } from "@/features/auth/roles";
import type {
  AuditLogEntry,
  AuditLogPage,
} from "@/features/tenant-config/types";
import { formatDateTime } from "@/lib/format";

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

interface AuditLogTableProps {
  page: AuditLogPage | undefined;
  isPending: boolean;
  isFetching?: boolean;
  error?: unknown;
  state: DataTableSearchState;
  onStateChange: (patch: Partial<DataTableSearchState>) => void;
}

/**
 * La tabla de un registro de auditoría — mismas columnas para las 3
 * pantallas que la usan (self-service del propio tenant, tenant visto por
 * el operador, organización vista por el operador). Solo cambia de dónde
 * sale `page` (cada una tiene su propio hook/endpoint).
 */
export function AuditLogTable({
  page,
  isPending,
  isFetching,
  error,
  state,
  onStateChange,
}: AuditLogTableProps) {
  return (
    <DataTable
      columns={columns}
      page={toPage(page)}
      isPending={isPending}
      isFetching={isFetching}
      error={error}
      state={state}
      onStateChange={onStateChange}
      searchable={false}
      emptyState={{ title: "Sin actividad registrada todavía." }}
      getRowId={(entry) => entry.id}
    />
  );
}
