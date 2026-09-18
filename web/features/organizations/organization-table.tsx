"use client";

import { useRouter } from "next/navigation";

import {
  DataTable,
  createAppColumnHelper,
  useTableSearchParams,
  type PagedResult,
} from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { planLabel } from "@/features/tenants/options";

import { CreateOrganizationDialog } from "./create-organization-dialog";
import { useOrganizations } from "./use-organizations";
import type { OrganizationPage, OrganizationSummary } from "./types";

const ch = createAppColumnHelper<OrganizationSummary>();

function statusLabel(status: string): string {
  return status === "Active" ? "Activa" : "Suspendida";
}

const columns = ch.columns([
  ch.accessor("name", { header: "Nombre" }),
  ch.accessor("slug", {
    header: "Slug",
    cell: (cell) => (
      <span className="text-muted-foreground font-mono text-xs">
        {cell.getValue()}
      </span>
    ),
  }),
  ch.display({
    id: "plan",
    header: "Plan",
    cell: (cell) => (
      <Badge variant="outline" className="font-normal">
        {planLabel(cell.row.original.plan)}
      </Badge>
    ),
  }),
  ch.display({
    id: "estado",
    header: "Estado",
    cell: (cell) => (
      <Badge
        variant={
          cell.row.original.status === "Active" ? "outline" : "secondary"
        }
      >
        {statusLabel(cell.row.original.status)}
      </Badge>
    ),
  }),
]);

function toPage(
  page: OrganizationPage | undefined,
): PagedResult<OrganizationSummary> | undefined {
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

export function OrganizationTable() {
  const router = useRouter();
  const [state, setState] = useTableSearchParams();
  const { data, isPending, isFetching, error } = useOrganizations(state);

  return (
    <DataTable
      columns={columns}
      page={toPage(data)}
      isPending={isPending}
      isFetching={isFetching}
      error={error}
      state={state}
      onStateChange={setState}
      searchPlaceholder="Nombre o slug…"
      emptyState={{ title: "No hay organizaciones dadas de alta." }}
      onRowClick={(org) => router.push(`/nemus/organizaciones/${org.id}`)}
      getRowId={(org) => org.id}
      toolbarActions={<CreateOrganizationDialog />}
    />
  );
}
