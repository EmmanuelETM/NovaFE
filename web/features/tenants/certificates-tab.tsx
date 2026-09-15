"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useController, useForm } from "react-hook-form";
import { z } from "zod";
import { Upload } from "lucide-react";

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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { applyFieldErrors } from "@/lib/api/form-errors";
import { formatDate } from "@/lib/format";
import { selectItems } from "@/lib/select-items";

import { ENVIRONMENT_OPTIONS } from "./options";
import {
  useRevokeCertificate,
  useTenantCertificates,
  useUploadCertificate,
} from "./use-tenant-certificates";
import { tenantErrorMessage } from "./use-tenants";

export function CertificatesTab({ tenantId }: { tenantId: string }) {
  const { data: certificates, isPending } = useTenantCertificates(tenantId);
  const revoke = useRevokeCertificate();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <UploadCertificateDialog tenantId={tenantId} />
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Ambiente</TableHead>
            <TableHead>Titular</TableHead>
            <TableHead>Vigencia</TableHead>
            <TableHead>Estado</TableHead>
            <TableHead className="text-right">Acciones</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {isPending ? (
            <TableRow>
              <TableCell
                colSpan={5}
                className="text-muted-foreground text-center"
              >
                Cargando…
              </TableCell>
            </TableRow>
          ) : !certificates || certificates.length === 0 ? (
            <TableRow>
              <TableCell
                colSpan={5}
                className="text-muted-foreground text-center"
              >
                Sin certificados cargados.
              </TableCell>
            </TableRow>
          ) : (
            certificates.map((certificate) => (
              <TableRow key={certificate.id}>
                <TableCell>{certificate.environment}</TableCell>
                <TableCell>{certificate.holderIdentifier}</TableCell>
                <TableCell>
                  {formatDate(certificate.validFrom)} –{" "}
                  {formatDate(certificate.validTo)}
                </TableCell>
                <TableCell>
                  <Badge
                    variant={
                      certificate.status === "Active"
                        ? "outline"
                        : "destructive"
                    }
                  >
                    {certificate.status}
                  </Badge>
                </TableCell>
                <TableCell className="text-right">
                  {certificate.status === "Active" && (
                    <Button
                      size="xs"
                      variant="ghost"
                      disabled={revoke.isPending}
                      onClick={() =>
                        revoke.mutate({ tenantId, id: certificate.id })
                      }
                    >
                      Revocar
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}

const schema = z.object({
  password: z.string().min(1, "La contraseña es obligatoria."),
  environment: z.string().min(1, "El ambiente es obligatorio."),
});

type Values = z.infer<typeof schema>;

function UploadCertificateDialog({ tenantId }: { tenantId: string }) {
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
        tenantId,
        file,
        password: values.password,
        environment: values.environment,
      });
      form.reset();
      setFile(null);
      setOpen(false);
    } catch (error) {
      if (!applyFieldErrors(error, form.setError)) {
        form.setError("password", { message: tenantErrorMessage(error) });
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
      <DialogTrigger render={<Button size="xs" />}>
        <Upload /> Cargar certificado
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cargar certificado</DialogTitle>
          <DialogDescription>
            El .p12/.pfx del contribuyente, su contraseña y el ambiente en el
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
