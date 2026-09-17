"use client";

import { useRouter } from "next/navigation";

import {
  DataTable,
  createAppColumnHelper,
  useTableSearchParams,
  type PagedResult,
} from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

import { useTenants } from "./use-tenants";
import type { TenantPage, TenantSummary } from "./types";

const ch = createAppColumnHelper<TenantSummary>();

const columns = ch.columns([
  ch.accessor("rnc", { header: "RNC", meta: { label: "RNC" } }),
  ch.accessor("legalName", { header: "Razón social" }),
  ch.accessor("plan", { header: "Plan" }),
  ch.display({
    id: "estado",
    header: "Estado",
    cell: (cell) => (
      <Badge
        variant={
          cell.row.original.status === "Active" ? "outline" : "secondary"
        }
      >
        {cell.row.original.status}
      </Badge>
    ),
  }),
]);

function toPage(
  page: TenantPage | undefined,
): PagedResult<TenantSummary> | undefined {
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

export function TenantTable() {
  const router = useRouter();
  const [state, setState] = useTableSearchParams();
  const { data, isPending, isFetching, error } = useTenants(state);

  return (
    <DataTable
      columns={columns}
      page={toPage(data)}
      isPending={isPending}
      isFetching={isFetching}
      error={error}
      state={state}
      onStateChange={setState}
      searchPlaceholder="RNC o razón social…"
      emptyState={{ title: "No hay contribuyentes dados de alta." }}
      onRowClick={(tenant) => router.push(`/nemus/tenants/${tenant.id}`)}
      getRowId={(tenant) => tenant.id}
      toolbarActions={
        <Button
          size="sm"
          className="h-8 gap-1.5"
          onClick={() => router.push("/nemus/tenants/nuevo")}
        >
          Nuevo contribuyente
        </Button>
      }
    />
  );
}
