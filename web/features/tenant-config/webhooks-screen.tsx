"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Check, Copy, ListOrdered, MoreHorizontal, Plus } from "lucide-react";

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { applyFieldErrors } from "@/lib/api/form-errors";
import {
  DataTable,
  STATIC_TABLE_STATE,
  createAppColumnHelper,
  toStaticPage,
} from "@/components/shared/data-table";

import { WEBHOOK_EVENT_GROUPS, webhookEventLabel } from "./webhook-events";
import { tenantConfigErrorMessage } from "./use-certificates";
import { WebhookDeliveriesDialog } from "./webhook-deliveries-dialog";
import {
  useCreateWebhook,
  useDeleteWebhook,
  usePingWebhook,
  useRotateWebhookSecret,
  useUpdateWebhook,
  useWebhooks,
} from "./use-webhooks";
import type { WebhookEndpoint } from "./types";

const MAX_EVENT_BADGES = 3;

const ch = createAppColumnHelper<WebhookEndpoint>();

function columnsFor(onViewDeliveries: (webhook: WebhookEndpoint) => void) {
  return ch.columns([
    ch.accessor("url", {
      header: "URL",
      cell: (cell) => (
        <span className="block max-w-64 truncate font-mono text-xs">
          {cell.getValue()}
        </span>
      ),
    }),
    ch.display({
      id: "eventos",
      header: "Eventos",
      cell: (cell) => <WebhookEvents webhook={cell.row.original} />,
    }),
    ch.display({
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
    ch.display({
      id: "acciones",
      header: "",
      meta: { align: "right" },
      cell: (cell) => (
        <WebhookActions
          webhook={cell.row.original}
          onViewDeliveries={() => onViewDeliveries(cell.row.original)}
        />
      ),
    }),
  ]);
}

export function WebhooksScreen() {
  const { data: webhooks, isPending, error } = useWebhooks();
  const [selected, setSelected] = useState<WebhookEndpoint | null>(null);

  return (
    <>
      <DataTable
        columns={columnsFor(setSelected)}
        page={toStaticPage(webhooks)}
        isPending={isPending}
        error={error}
        state={STATIC_TABLE_STATE}
        onStateChange={() => {}}
        searchable={false}
        emptyState={{
          title: "Sin webhooks registrados.",
          description:
            "Registrá un endpoint para que te avisemos en tiempo real del ciclo de vida de tus e-CF.",
        }}
        toolbarActions={<CreateWebhookDialog />}
        getRowId={(webhook) => webhook.id}
      />

      <WebhookDeliveriesDialog
        webhook={selected}
        onClose={() => setSelected(null)}
      />
    </>
  );
}

function WebhookEvents({ webhook }: { webhook: WebhookEndpoint }) {
  const shownEvents = webhook.events.slice(0, MAX_EVENT_BADGES);
  const extraCount = webhook.events.length - shownEvents.length;

  return (
    <div className="flex flex-wrap gap-1">
      {shownEvents.map((event) => (
        <Badge key={event} variant="secondary">
          {webhookEventLabel(event)}
        </Badge>
      ))}
      {extraCount > 0 && <Badge variant="outline">+{extraCount}</Badge>}
    </div>
  );
}

function WebhookActions({
  webhook,
  onViewDeliveries,
}: {
  webhook: WebhookEndpoint;
  onViewDeliveries: () => void;
}) {
  const ping = usePingWebhook();
  const update = useUpdateWebhook();
  const remove = useDeleteWebhook();
  const rotate = useRotateWebhookSecret();

  const [rotatedSecret, setRotatedSecret] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const busy =
    ping.isPending || update.isPending || remove.isPending || rotate.isPending;

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger
          render={<Button variant="ghost" size="xs" aria-label="Acciones" />}
        >
          <MoreHorizontal />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onClick={onViewDeliveries}>
            <ListOrdered /> Ver entregas
          </DropdownMenuItem>
          <DropdownMenuItem
            disabled={busy}
            onClick={() => ping.mutate(webhook.id)}
          >
            Probar (ping)
          </DropdownMenuItem>
          <DropdownMenuItem
            disabled={busy}
            onClick={() =>
              update.mutate({ id: webhook.id, enabled: !webhook.enabled })
            }
          >
            {webhook.enabled ? "Deshabilitar" : "Habilitar"}
          </DropdownMenuItem>
          <DropdownMenuItem
            disabled={busy}
            onClick={async () => {
              const result = await rotate.mutateAsync(webhook.id);
              setRotatedSecret(result.secret);
            }}
          >
            Rotar secret
          </DropdownMenuItem>
          <DropdownMenuItem
            disabled={busy}
            onClick={() => setConfirmDelete(true)}
          >
            Eliminar
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <RotatedSecretDialog
        secret={rotatedSecret}
        onClose={() => setRotatedSecret(null)}
      />

      <AlertDialog open={confirmDelete} onOpenChange={setConfirmDelete}>
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>Eliminar este webhook</AlertDialogTitle>
            <AlertDialogDescription>
              Dejará de recibir eventos en «{webhook.url}». No se puede
              deshacer.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setConfirmDelete(false);
                remove.mutate(webhook.id);
              }}
            >
              Eliminar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

/** El diálogo "mostrar una vez + copiar" — mismo patrón que `api-keys-tab.tsx`. */
function SecretReveal({ secret }: { secret: string }) {
  const [copied, setCopied] = useState(false);

  return (
    <div className="flex items-center gap-2">
      <Input readOnly value={secret} className="font-mono text-xs" />
      <Button
        type="button"
        size="icon"
        variant="outline"
        onClick={() => {
          void navigator.clipboard.writeText(secret);
          setCopied(true);
        }}
      >
        {copied ? <Check /> : <Copy />}
      </Button>
    </div>
  );
}

/** Se abre sola cuando llega un `secret` (tras rotarlo) y se cierra con `onClose`. */
function RotatedSecretDialog({
  secret,
  onClose,
}: {
  secret: string | null;
  onClose: () => void;
}) {
  return (
    <Dialog open={secret !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Nuevo secret</DialogTitle>
          <DialogDescription>
            Esta es la <b>única</b> vez que se puede ver. Cópialo y actualizá la
            verificación de firma en tu endpoint.
          </DialogDescription>
        </DialogHeader>

        {secret && <SecretReveal secret={secret} />}

        <DialogFooter>
          <Button type="button" onClick={onClose}>
            Listo
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

const schema = z.object({
  url: z
    .string()
    .min(1, "La URL de destino es obligatoria.")
    .url("Tiene que ser una URL absoluta (http o https)."),
  description: z.string(),
});

type Values = z.infer<typeof schema>;

function CreateWebhookDialog() {
  const [open, setOpen] = useState(false);
  const [events, setEvents] = useState<string[]>([]);
  const [eventsError, setEventsError] = useState<string | null>(null);
  const [secret, setSecret] = useState<string | null>(null);
  const create = useCreateWebhook();

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { url: "", description: "" },
  });

  const toggleEvent = (value: string, checked: boolean) => {
    setEvents((current) =>
      checked ? [...current, value] : current.filter((v) => v !== value),
    );
  };

  const submit = form.handleSubmit(async (values) => {
    if (events.length === 0) {
      setEventsError("Hay que suscribirse al menos a un evento.");
      return;
    }
    setEventsError(null);

    try {
      const created = await create.mutateAsync({
        url: values.url.trim(),
        events,
        description: values.description.trim() || undefined,
      });
      setSecret(created.secret);
    } catch (error) {
      if (!applyFieldErrors(error, form.setError)) {
        form.setError("url", { message: tenantConfigErrorMessage(error) });
      }
    }
  });

  const close = () => {
    setOpen(false);
    form.reset();
    setEvents([]);
    setEventsError(null);
    setSecret(null);
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => (next ? setOpen(true) : close())}
    >
      <DialogTrigger render={<Button size="sm" className="h-8 gap-1.5" />}>
        <Plus /> Nuevo webhook
      </DialogTrigger>

      <DialogContent>
        {secret ? (
          <>
            <DialogHeader>
              <DialogTitle>Webhook registrado</DialogTitle>
              <DialogDescription>
                Esta es la <b>única</b> vez que se puede ver el secret. Cópialo
                y guárdalo para verificar la firma de las entregas.
              </DialogDescription>
            </DialogHeader>

            <SecretReveal secret={secret} />

            <DialogFooter>
              <Button type="button" onClick={close}>
                Listo
              </Button>
            </DialogFooter>
          </>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle>Nuevo webhook</DialogTitle>
              <DialogDescription>
                La URL de destino y a qué eventos se suscribe.
              </DialogDescription>
            </DialogHeader>

            <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
              <Field>
                <FieldLabel htmlFor="webhook-url">URL de destino</FieldLabel>
                <Input
                  id="webhook-url"
                  placeholder="https://tu-erp.com/webhooks/novafe"
                  {...form.register("url")}
                />
                <FieldError errors={[form.formState.errors.url]} />
              </Field>

              <Field>
                <FieldLabel htmlFor="webhook-description">
                  Descripción (opcional)
                </FieldLabel>
                <Input
                  id="webhook-description"
                  {...form.register("description")}
                />
                <FieldError errors={[form.formState.errors.description]} />
              </Field>

              <div className="flex flex-col gap-3">
                <span className="text-sm font-medium">Eventos</span>
                {WEBHOOK_EVENT_GROUPS.map((group) => (
                  <div key={group.label} className="flex flex-col gap-1.5">
                    <span className="text-muted-foreground text-xs">
                      {group.label}
                    </span>
                    <div className="flex flex-col gap-1.5 pl-1">
                      {group.events.map((event) => (
                        <label
                          key={event.value}
                          className="flex items-center gap-2 text-sm"
                        >
                          <Checkbox
                            checked={events.includes(event.value)}
                            onCheckedChange={(checked) =>
                              toggleEvent(event.value, checked === true)
                            }
                          />
                          {event.label}
                        </label>
                      ))}
                    </div>
                  </div>
                ))}
                <FieldError
                  errors={[eventsError ? { message: eventsError } : undefined]}
                />
              </div>

              <DialogFooter>
                <DialogClose
                  render={<Button type="button" variant="outline" />}
                >
                  Cancelar
                </DialogClose>
                <Button type="submit" disabled={form.formState.isSubmitting}>
                  Registrar
                </Button>
              </DialogFooter>
            </form>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
