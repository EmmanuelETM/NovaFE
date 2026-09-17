"use client";

import { useState } from "react";
import { ListOrdered } from "lucide-react";

import {
  DataTable,
  DEFAULT_PAGE_SIZE,
  STATIC_TABLE_STATE,
  createAppColumnHelper,
  toStaticPage,
  type DataTableSearchState,
  type PagedResult,
} from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { webhookEventLabel } from "@/features/tenant-config/webhook-events";
import type {
  WebhookDelivery,
  WebhookDeliveryPage,
  WebhookEndpoint,
} from "@/features/tenant-config/types";
import { formatDateTime } from "@/lib/format";

import {
  useRetryTenantWebhookDelivery,
  useTenantWebhookDeliveries,
  useTenantWebhooks,
} from "./use-tenant-webhooks";

const MAX_EVENT_BADGES = 3;

const ech = createAppColumnHelper<WebhookEndpoint>();

function endpointColumnsFor(
  onViewDeliveries: (webhook: WebhookEndpoint) => void,
) {
  return ech.columns([
    ech.accessor("url", {
      header: "URL",
      cell: (cell) => (
        <span className="block max-w-64 truncate font-mono text-xs">
          {cell.getValue()}
        </span>
      ),
    }),
    ech.display({
      id: "eventos",
      header: "Eventos",
      cell: (cell) => {
        const events = cell.row.original.events.slice(0, MAX_EVENT_BADGES);
        const extraCount = cell.row.original.events.length - events.length;
        return (
          <div className="flex flex-wrap gap-1">
            {events.map((event) => (
              <Badge key={event} variant="secondary">
                {webhookEventLabel(event)}
              </Badge>
            ))}
            {extraCount > 0 && <Badge variant="outline">+{extraCount}</Badge>}
          </div>
        );
      },
    }),
    ech.display({
      id: "estado",
      header: "Estado",
      cell: (cell) => (
        <div className="flex items-center gap-2">
          <Badge variant={cell.row.original.enabled ? "outline" : "secondary"}>
            {cell.row.original.enabled ? "Activo" : "Deshabilitado"}
          </Badge>
          {Number(cell.row.original.consecutiveFailures) > 0 && (
            <Badge variant="destructive">
              {cell.row.original.consecutiveFailures} fallos
            </Badge>
          )}
        </div>
      ),
    }),
    ech.display({
      id: "acciones",
      header: "",
      meta: { align: "right" },
      cell: (cell) => (
        <Button
          size="xs"
          variant="ghost"
          onClick={() => onViewDeliveries(cell.row.original)}
        >
          <ListOrdered /> Ver entregas
        </Button>
      ),
    }),
  ]);
}

/**
 * Los webhooks de un contribuyente, vistos por el operador — solo lectura y
 * reintento de entregas muertas. Crear/editar/rotar/eliminar no tiene un
 * caso de uso de soporte real, así que no hay acciones de configuración acá
 * (a diferencia de `WebhooksScreen`, self-service).
 */
export function WebhooksTab({ tenantId }: { tenantId: string }) {
  const { data: webhooks, isPending, error } = useTenantWebhooks(tenantId);
  const [selected, setSelected] = useState<WebhookEndpoint | null>(null);

  return (
    <>
      <DataTable
        columns={endpointColumnsFor(setSelected)}
        page={toStaticPage(webhooks)}
        isPending={isPending}
        error={error}
        state={STATIC_TABLE_STATE}
        onStateChange={() => {}}
        searchable={false}
        emptyState={{ title: "Sin webhooks registrados." }}
        getRowId={(webhook) => webhook.id}
      />

      <TenantWebhookDeliveriesDialog
        tenantId={tenantId}
        webhook={selected}
        onClose={() => setSelected(null)}
      />
    </>
  );
}

const dch = createAppColumnHelper<WebhookDelivery>();

function statusVariant(
  status: string,
): "outline" | "secondary" | "destructive" {
  if (status === "delivered") return "outline";
  if (status === "dead") return "destructive";
  return "secondary";
}

function statusLabel(status: string): string {
  switch (status) {
    case "delivered":
      return "Entregada";
    case "dead":
      return "Muerta";
    case "pending":
      return "Pendiente";
    case "processing":
      return "En curso";
    default:
      return status;
  }
}

function toPage(
  page: WebhookDeliveryPage | undefined,
): PagedResult<WebhookDelivery> | undefined {
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

function RetryButton({
  tenantId,
  endpointId,
  delivery,
}: {
  tenantId: string;
  endpointId: string;
  delivery: WebhookDelivery;
}) {
  const retry = useRetryTenantWebhookDelivery(tenantId, endpointId);

  if (delivery.status !== "dead") return null;

  return (
    <Button
      size="xs"
      variant="ghost"
      disabled={retry.isPending}
      onClick={() => retry.mutate(delivery.id)}
    >
      Reintentar
    </Button>
  );
}

function deliveryColumnsFor(tenantId: string, endpointId: string) {
  return dch.columns([
    dch.accessor("eventType", {
      header: "Evento",
      cell: (cell) => webhookEventLabel(cell.getValue()),
    }),
    dch.display({
      id: "estado",
      header: "Estado",
      cell: (cell) => (
        <Badge variant={statusVariant(cell.row.original.status)}>
          {statusLabel(cell.row.original.status)}
        </Badge>
      ),
    }),
    dch.accessor("attempts", { header: "Intentos" }),
    dch.display({
      id: "ultimoCodigo",
      header: "Último código",
      cell: (cell) => cell.row.original.lastStatusCode ?? "—",
    }),
    dch.accessor("createdAt", {
      header: "Creada",
      cell: (cell) => formatDateTime(cell.getValue()),
    }),
    dch.display({
      id: "acciones",
      header: "",
      meta: { align: "right" },
      cell: (cell) => (
        <RetryButton
          tenantId={tenantId}
          endpointId={endpointId}
          delivery={cell.row.original}
        />
      ),
    }),
  ]);
}

function TenantWebhookDeliveriesDialog({
  tenantId,
  webhook,
  onClose,
}: {
  tenantId: string;
  webhook: WebhookEndpoint | null;
  onClose: () => void;
}) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  const { data, isPending, isFetching, error } = useTenantWebhookDeliveries(
    tenantId,
    webhook?.id ?? "",
    page,
    pageSize,
  );

  const state: DataTableSearchState = {
    page,
    pageSize,
    search: "",
    sort: null,
    filters: {},
  };

  return (
    <Dialog
      open={webhook !== null}
      onOpenChange={(next) => {
        if (!next) {
          onClose();
          setPage(1);
        }
      }}
    >
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Entregas</DialogTitle>
          <DialogDescription>
            {webhook && <>Log de entregas de «{webhook.url}».</>}
          </DialogDescription>
        </DialogHeader>

        {webhook && (
          <DataTable
            columns={deliveryColumnsFor(tenantId, webhook.id)}
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
            emptyState={{ title: "Sin entregas registradas todavía." }}
            getRowId={(delivery) => delivery.id}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}
