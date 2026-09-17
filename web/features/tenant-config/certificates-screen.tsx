"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useController, useForm } from "react-hook-form";
import { z } from "zod";
import { Upload } from "lucide-react";

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
import { applyFieldErrors } from "@/lib/api/form-errors";
import { ENVIRONMENT_OPTIONS } from "@/features/tenants/options";
import { formatDate } from "@/lib/format";
import { selectItems } from "@/lib/select-items";

import {
  tenantConfigErrorMessage,
  useCertificates,
  useRevokeCertificate,
  useUploadCertificate,
} from "./use-certificates";
import type { Certificate } from "./types";

const ch = createAppColumnHelper<Certificate>();

const columns = ch.columns([
  ch.accessor("environment", { header: "Ambiente" }),
  ch.accessor("holderIdentifier", { header: "Titular" }),
  ch.display({
    id: "vigencia",
    header: "Vigencia",
    cell: (cell) =>
      `${formatDate(cell.row.original.validFrom)} – ${formatDate(cell.row.original.validTo)}`,
  }),
  ch.display({
    id: "estado",
    header: "Estado",
    cell: (cell) => (
      <Badge
        variant={
          cell.row.original.status === "Active" ? "outline" : "destructive"
        }
      >
        {cell.row.original.status}
      </Badge>
    ),
  }),
  ch.display({
    id: "acciones",
    header: "",
    meta: { align: "right" },
    cell: (cell) => <CertificateActions certificate={cell.row.original} />,
  }),
]);

export function CertificatesScreen() {
  const { data: certificates, isPending, error } = useCertificates();

  return (
    <DataTable
      columns={columns}
      page={toStaticPage(certificates)}
      isPending={isPending}
      error={error}
      state={STATIC_TABLE_STATE}
      onStateChange={() => {}}
      searchable={false}
      emptyState={{ title: "Sin certificados cargados." }}
      toolbarActions={<UploadCertificateDialog />}
      getRowId={(certificate) => certificate.id}
    />
  );
}

function CertificateActions({ certificate }: { certificate: Certificate }) {
  const revoke = useRevokeCertificate();

  if (certificate.status !== "Active") return null;

  return (
    <Button
      size="xs"
      variant="ghost"
      disabled={revoke.isPending}
      onClick={() => revoke.mutate(certificate.id)}
    >
      Revocar
    </Button>
  );
}

const schema = z.object({
  password: z.string().min(1, "La contraseña es obligatoria."),
  environment: z.string().min(1, "El ambiente es obligatorio."),
});

type Values = z.infer<typeof schema>;

function UploadCertificateDialog() {
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const upload = useUploadCertificate();

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { password: "", environment: "Test" },
  });
  const environment = useController({
    control: form.control,
    name: "environment",
  });

  const submit = form.handleSubmit(async (values) => {
    if (!file) {
      setFileError("El archivo .p12/.pfx es obligatorio.");
      return;
    }
    setFileError(null);

    try {
      await upload.mutateAsync({
        file,
        password: values.password,
        environment: values.environment,
      });
      form.reset();
      setFile(null);
      setOpen(false);
    } catch (error) {
      if (!applyFieldErrors(error, form.setError)) {
        form.setError("password", { message: tenantConfigErrorMessage(error) });
      }
    }
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) {
          form.reset();
          setFile(null);
          setFileError(null);
        }
      }}
    >
      <DialogTrigger render={<Button size="sm" className="h-8 gap-1.5" />}>
        <Upload /> Cargar certificado
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cargar certificado</DialogTitle>
          <DialogDescription>
            El .p12/.pfx de tu contribuyente, su contraseña y el ambiente en el
            que va a operar.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
          <Field>
            <FieldLabel htmlFor="certificate-file">
              Archivo (.p12/.pfx)
            </FieldLabel>
            <Input
              id="certificate-file"
              type="file"
              accept=".p12,.pfx"
              onChange={(event) => setFile(event.target.files?.[0] ?? null)}
            />
            <FieldError
              errors={[fileError ? { message: fileError } : undefined]}
            />
          </Field>

          <Field>
            <FieldLabel htmlFor="certificate-password">Contraseña</FieldLabel>
            <Input
              id="certificate-password"
              type="password"
              autoComplete="off"
              {...form.register("password")}
            />
            <FieldError errors={[form.formState.errors.password]} />
          </Field>

          <Field>
            <FieldLabel htmlFor="certificate-environment">Ambiente</FieldLabel>
            <Select
              items={selectItems(ENVIRONMENT_OPTIONS)}
              value={environment.field.value}
              onValueChange={(next) => environment.field.onChange(String(next))}
            >
              <SelectTrigger id="certificate-environment" className="w-full">
                <SelectValue />
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

          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>
              Cancelar
            </DialogClose>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              Cargar
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
