"use client";

import { useState } from "react";

import {
  DataTable,
  DEFAULT_PAGE_SIZE,
  createAppColumnHelper,
  type DataTableSearchState,
  type PagedResult,
} from "@/components/shared/data-table";
import { Button } from "@/components/ui/button";
import { webhookEventLabel } from "@/features/tenant-config/webhook-events";
import { useRetryTenantWebhookDelivery } from "@/features/tenants/use-tenant-webhooks";
import { formatDateTime } from "@/lib/format";

import { useDeadDeliveries } from "./use-dead-deliveries";
import type { DeadWebhookDelivery, DeadWebhookDeliveryPage } from "./types";

function toPage(
  page: DeadWebhookDeliveryPage | undefined,
): PagedResult<DeadWebhookDelivery> | undefined {
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

function RetryButton({ delivery }: { delivery: DeadWebhookDelivery }) {
  const retry = useRetryTenantWebhookDelivery(
    delivery.tenantId,
    delivery.endpointId,
  );

  return (
    <Button
      size="xs"
      variant="ghost"
      disabled={retry.isPending}
      onClick={() => retry.mutate(delivery.deliveryId)}
    >
      Reintentar
    </Button>
  );
}

const ch = createAppColumnHelper<DeadWebhookDelivery>();

const columns = ch.columns([
  ch.display({
    id: "contribuyente",
    header: "Contribuyente",
    cell: (cell) => (
      <div className="flex flex-col">
        <span className="text-sm">{cell.row.original.legalName}</span>
        <span className="text-muted-foreground font-mono text-xs">
          {cell.row.original.rnc}
        </span>
      </div>
    ),
  }),
  ch.accessor("url", {
    header: "Endpoint",
    cell: (cell) => (
      <span className="block max-w-56 truncate font-mono text-xs">
        {cell.getValue()}
      </span>
    ),
  }),
  ch.accessor("eventType", {
    header: "Evento",
    cell: (cell) => webhookEventLabel(cell.getValue()),
  }),
  ch.accessor("attempts", { header: "Intentos" }),
  ch.accessor("updatedAt", {
    header: "Última actualización",
    cell: (cell) => formatDateTime(cell.getValue()),
  }),
  ch.display({
    id: "acciones",
    header: "",
    meta: { align: "right" },
    cell: (cell) => <RetryButton delivery={cell.row.original} />,
  }),
]);

/**
 * Entregas de webhook `dead` de toda la plataforma. No se pinta nada si no
 * hay ninguna — no vale la pena mostrar ruido a un estado sano en un
 * dashboard que ya tiene bastante que mirar. Sin `Card`: `DataTable` ya trae
 * su propio borde/rounded, anidarla en una tarjeta duplicaría el marco.
 */
export function DeadDeliveriesCard() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const { data, isPending, isFetching, error } = useDeadDeliveries(
    page,
    pageSize,
  );

  if (!isPending && !error && Number(data?.totalCount ?? 0) === 0) {
    return null;
  }

  const state: DataTableSearchState = {
    page,
    pageSize,
    search: "",
    sort: null,
    filters: {},
  };

  return (
    <div className="flex flex-col gap-2">
      <h2 className="text-sm font-medium">Entregas de webhook muertas</h2>
      <DataTable
        columns={columns}
        page={toPage(data)}
        isPending={isPending}
        isFetching={isFetching}
        error={error}
        state={state}
        onStateChange={(patch) => {
          if (patch.page !== undefined) setPage(patch.page);
          if (patch.pageSize !== undefined) setPageSize(patch.pageSize);
        }}
        searchable={false}
        emptyState={{ title: "Sin entregas muertas." }}
        getRowId={(delivery) => delivery.deliveryId}
      />
    </div>
  );
}
