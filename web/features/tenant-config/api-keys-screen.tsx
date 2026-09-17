"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useController, useForm } from "react-hook-form";
import { z } from "zod";
import { Check, Copy, KeyRound } from "lucide-react";

import {
  DataTable,
  STATIC_TABLE_STATE,
  createAppColumnHelper,
  toStaticPage,
} from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { roleLabel } from "@/features/auth/roles";
import { TENANT_ROLE_OPTIONS } from "@/features/users/role-options";
import { ENVIRONMENT_OPTIONS } from "@/features/tenants/options";
import { applyFieldErrors } from "@/lib/api/form-errors";
import { formatDate } from "@/lib/format";
import { selectItems } from "@/lib/select-items";

import { tenantConfigErrorMessage } from "./use-certificates";
import { useApiKeys, useCreateApiKey, useRevokeApiKey } from "./use-api-keys";
import type { ApiKey } from "./types";

function isActive(key: ApiKey): boolean {
  return !key.revokedAt;
}

const ch = createAppColumnHelper<ApiKey>();

const columns = ch.columns([
  ch.accessor("label", { header: "Etiqueta" }),
  ch.accessor("prefix", {
    header: "Prefijo",
    cell: (cell) => (
      <span className="font-mono text-xs">{cell.getValue()}</span>
    ),
  }),
  ch.accessor("environment", { header: "Ambiente" }),
  ch.display({
    id: "rol",
    header: "Rol",
    cell: (cell) => roleLabel(cell.row.original.role),
  }),
  ch.display({
    id: "vencimiento",
    header: "Vencimiento",
    cell: (cell) =>
      cell.row.original.expiresAt
        ? formatDate(cell.row.original.expiresAt)
        : "Sin vencimiento",
  }),
  ch.display({
    id: "estado",
    header: "Estado",
    cell: (cell) => (
      <Badge variant={isActive(cell.row.original) ? "outline" : "destructive"}>
        {isActive(cell.row.original) ? "Activa" : "Revocada"}
      </Badge>
    ),
  }),
  ch.display({
    id: "acciones",
    header: "",
    meta: { align: "right" },
    cell: (cell) => <ApiKeyActions apiKey={cell.row.original} />,
  }),
]);

function ApiKeyActions({ apiKey }: { apiKey: ApiKey }) {
  const revoke = useRevokeApiKey();

  if (!isActive(apiKey)) return null;

  return (
    <Button
      size="xs"
      variant="ghost"
      disabled={revoke.isPending}
      onClick={() => revoke.mutate(apiKey.id)}
    >
      Revocar
    </Button>
  );
}

/** Las API keys del tenant activo: acuñar, listar, revocar. Self-service. */
export function ApiKeysScreen() {
  const { data: keys, isPending, error } = useApiKeys();

  return (
    <DataTable
      columns={columns}
      page={toStaticPage(keys)}
      isPending={isPending}
      error={error}
      state={STATIC_TABLE_STATE}
      onStateChange={() => {}}
      searchable={false}
      emptyState={{
        title: "Sin API keys acuñadas.",
        description:
          "Tu ERP necesita una para emitir contra NovaFE — creá la primera.",
      }}
      toolbarActions={<CreateApiKeyDialog />}
      getRowId={(key) => key.id}
    />
  );
}

const schema = z.object({
  label: z.string(),
  environment: z.string(),
  role: z.string().min(1, "El rol es obligatorio."),
});

type Values = z.infer<typeof schema>;

function CreateApiKeyDialog() {
  const [open, setOpen] = useState(false);
  const [token, setToken] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const create = useCreateApiKey();

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { label: "", environment: "", role: "admin_tenant" },
  });
  const environment = useController({
    control: form.control,
    name: "environment",
  });
  const role = useController({ control: form.control, name: "role" });

  const submit = form.handleSubmit(async (values) => {
    try {
      const created = await create.mutateAsync({
        label: values.label.trim() || null,
        environment: values.environment || null,
        role: values.role,
      });
      setToken(created.token);
    } catch (error) {
      if (!applyFieldErrors(error, form.setError)) {
        form.setError("role", { message: tenantConfigErrorMessage(error) });
      }
    }
  });

  const close = () => {
    setOpen(false);
    form.reset();
    setToken(null);
    setCopied(false);
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => (next ? setOpen(true) : close())}
    >
      <DialogTrigger render={<Button size="sm" className="h-8 gap-1.5" />}>
        <KeyRound /> Nueva API key
      </DialogTrigger>

      <DialogContent>
        {token ? (
          <>
            <DialogHeader>
              <DialogTitle>API key acuñada</DialogTitle>
              <DialogDescription>
                Esta es la <b>única</b> vez que se puede ver el token. Cópialo y
                guárdalo en un lugar seguro.
              </DialogDescription>
            </DialogHeader>

            <div className="flex items-center gap-2">
              <Input readOnly value={token} className="font-mono text-xs" />
              <Button
                type="button"
                size="icon"
                variant="outline"
                onClick={() => {
                  void navigator.clipboard.writeText(token);
                  setCopied(true);
                }}
              >
                {copied ? <Check /> : <Copy />}
              </Button>
            </div>

            <DialogFooter>
              <Button type="button" onClick={close}>
                Listo
              </Button>
            </DialogFooter>
          </>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle>Nueva API key</DialogTitle>
              <DialogDescription>
                Requiere certificado activo y un rango de e-NCF para el ambiente
                elegido — si falta alguno, la petición lo dice.
              </DialogDescription>
            </DialogHeader>

            <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
              <Field>
                <FieldLabel htmlFor="apikey-label">
                  Etiqueta (opcional)
                </FieldLabel>
                <Input id="apikey-label" {...form.register("label")} />
                <FieldError errors={[form.formState.errors.label]} />
              </Field>

              <Field>
                <FieldLabel htmlFor="apikey-environment">
                  Ambiente (vacío = el del perfil de emisor)
                </FieldLabel>
                <Select
                  items={selectItems(ENVIRONMENT_OPTIONS)}
                  value={environment.field.value}
                  onValueChange={(next) =>
                    environment.field.onChange(String(next))
                  }
                >
                  <SelectTrigger id="apikey-environment" className="w-full">
                    <SelectValue placeholder="Ambiente por defecto del perfil" />
                  </SelectTrigger>
                  <SelectContent>
                    {ENVIRONMENT_OPTIONS.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldError errors={[form.formState.errors.environment]} />
              </Field>

              <Field>
                <FieldLabel htmlFor="apikey-role">Rol</FieldLabel>
                <Select
                  items={selectItems(TENANT_ROLE_OPTIONS)}
                  value={role.field.value}
                  onValueChange={(next) => role.field.onChange(String(next))}
                >
                  <SelectTrigger id="apikey-role" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {TENANT_ROLE_OPTIONS.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldError errors={[form.formState.errors.role]} />
              </Field>

              <DialogFooter>
                <DialogClose
                  render={<Button type="button" variant="outline" />}
                >
                  Cancelar
                </DialogClose>
                <Button type="submit" disabled={form.formState.isSubmitting}>
                  Acuñar
                </Button>
              </DialogFooter>
            </form>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
