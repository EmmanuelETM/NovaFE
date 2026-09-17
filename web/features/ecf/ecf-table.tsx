"use client";

import { useRouter } from "next/navigation";

import {
  DataTable,
  createAppColumnHelper,
  useTableSearchParams,
  type DataTableFilter,
  type PagedResult,
} from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { useTenantId } from "@/features/auth/use-tenant-id";
import { formatCalendarDate, formatMoney } from "@/lib/format";

import {
  ECF_STATUS_LABELS,
  ECF_TYPE_LABELS,
  ecfStatusLabel,
  ecfStatusVariant,
  ecfTypeLabel,
  type EcfPage,
  type EcfSummary,
} from "./types";
import { useEcfList } from "./use-ecf";

const ch = createAppColumnHelper<EcfSummary>();

const columns = ch.columns([
  ch.accessor("encf", { header: "e-NCF", meta: { label: "e-NCF" } }),
  ch.accessor("type", {
    header: "Tipo",
    cell: (cell) => ecfTypeLabel(cell.getValue()),
  }),
  ch.display({
    id: "estado",
    header: "Estado",
    cell: (cell) => (
      <Badge variant={ecfStatusVariant(cell.row.original.status)}>
        {ecfStatusLabel(cell.row.original.status)}
      </Badge>
    ),
  }),
  ch.accessor("issueDate", {
    header: "Fecha de emisión",
    cell: (cell) => formatCalendarDate(cell.getValue()),
  }),
  ch.display({
    id: "comprador",
    header: "Comprador",
    cell: (cell) => {
      const { buyerName, buyerRnc } = cell.row.original;
      if (!buyerName && !buyerRnc) return "—";
      return (
        <div className="flex flex-col">
          {buyerName && <span className="truncate">{buyerName}</span>}
          {buyerRnc && (
            <span className="text-muted-foreground text-xs">{buyerRnc}</span>
          )}
        </div>
      );
    },
  }),
  ch.accessor("montoTotal", {
    header: "Monto",
    meta: { align: "right" },
    cell: (cell) => formatMoney(Number(cell.getValue())),
  }),
]);

const FILTERS: DataTableFilter[] = [
  {
    key: "type",
    label: "Tipo",
    options: Object.entries(ECF_TYPE_LABELS).map(([value, label]) => ({
      value,
      label,
    })),
  },
  {
    key: "status",
    label: "Estado",
    options: Object.entries(ECF_STATUS_LABELS).map(([value, label]) => ({
      value,
      label,
    })),
  },
];

function toPage(
  page: EcfPage | undefined,
): PagedResult<EcfSummary> | undefined {
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

export function EcfTable() {
  const router = useRouter();
  const tenantId = useTenantId();
  const [state, setState] = useTableSearchParams(["type", "status"]);
  const { data, isPending, isFetching, error } = useEcfList(state);

  return (
    <DataTable
      columns={columns}
      page={toPage(data)}
      isPending={isPending}
      isFetching={isFetching}
      error={error}
      state={state}
      onStateChange={setState}
      filters={FILTERS}
      searchPlaceholder="e-NCF, RNC o razón social…"
      emptyState={{ title: "No hay comprobantes emitidos todavía." }}
      onRowClick={(ecf) =>
        router.push(`/tenant/${tenantId}/comprobantes/${ecf.id}`)
      }
      getRowId={(ecf) => ecf.id}
    />
  );
}
