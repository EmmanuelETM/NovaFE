"use client";

import { useState } from "react";

import {
  DataTable,
  DEFAULT_PAGE_SIZE,
  createAppColumnHelper,
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
import { formatDateTime } from "@/lib/format";
import { webhookEventLabel } from "./webhook-events";
import {
  useRetryWebhookDelivery,
  useWebhookDeliveries,
} from "./use-webhook-deliveries";
import type {
  WebhookDelivery,
  WebhookDeliveryPage,
  WebhookEndpoint,
} from "./types";

const ch = createAppColumnHelper<WebhookDelivery>();

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
  endpointId,
  delivery,
}: {
  endpointId: string;
  delivery: WebhookDelivery;
}) {
  const retry = useRetryWebhookDelivery(endpointId);

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

function columnsFor(endpointId: string) {
  return ch.columns([
    ch.accessor("eventType", {
      header: "Evento",
      cell: (cell) => webhookEventLabel(cell.getValue()),
    }),
    ch.display({
      id: "estado",
      header: "Estado",
      cell: (cell) => (
        <Badge variant={statusVariant(cell.row.original.status)}>
          {statusLabel(cell.row.original.status)}
        </Badge>
      ),
    }),
    ch.accessor("attempts", { header: "Intentos" }),
    ch.display({
      id: "ultimoCodigo",
      header: "Último código",
      cell: (cell) => cell.row.original.lastStatusCode ?? "—",
    }),
    ch.accessor("createdAt", {
      header: "Creada",
      cell: (cell) => formatDateTime(cell.getValue()),
    }),
    ch.display({
      id: "acciones",
      header: "",
      meta: { align: "right" },
      cell: (cell) => (
        <RetryButton endpointId={endpointId} delivery={cell.row.original} />
      ),
    }),
  ]);
}

interface WebhookDeliveriesDialogProps {
  webhook: WebhookEndpoint | null;
  onClose: () => void;
}

/**
 * El log de entregas de un webhook, con reintento manual de las `dead` —
 * antes fuera de alcance (v1), ver `docs/webhooks.md`. Se abre sola cuando
 * hay un `webhook` (patrón "controlado por presencia", igual que
 * `RotatedSecretDialog`).
 */
export function WebhookDeliveriesDialog({
  webhook,
  onClose,
}: WebhookDeliveriesDialogProps) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  const { data, isPending, isFetching, error } = useWebhookDeliveries(
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
            {webhook && (
              <>
                Log de entregas de «{webhook.url}». Las entregas «muertas»
                agotaron la escalera de reintento automático — se pueden
                reintentar a mano.
              </>
            )}
          </DialogDescription>
        </DialogHeader>

        {webhook && (
          <DataTable
            columns={columnsFor(webhook.id)}
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
