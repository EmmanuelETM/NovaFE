"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useController, useForm } from "react-hook-form";
import { z } from "zod";
import { Plus } from "lucide-react";

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
import { formatCount } from "@/lib/format";
import { selectItems } from "@/lib/select-items";

import { ECF_TYPE_OPTIONS, ENVIRONMENT_OPTIONS } from "./options";
import {
  useRegisterSequence,
  useTenantSequences,
} from "./use-tenant-sequences";
import { tenantErrorMessage } from "./use-tenants";

export function SequencesTab({ tenantId }: { tenantId: string }) {
  const { data: sequences, isPending } = useTenantSequences(tenantId);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <RegisterSequenceDialog tenantId={tenantId} />
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Ambiente</TableHead>
            <TableHead>Tipo</TableHead>
            <TableHead>Serie</TableHead>
            <TableHead>Rango</TableHead>
            <TableHead>Restantes</TableHead>
            <TableHead>Estado</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {isPending ? (
            <TableRow>
              <TableCell
                colSpan={6}
                className="text-muted-foreground text-center"
              >
                Cargando…
              </TableCell>
            </TableRow>
          ) : !sequences || sequences.length === 0 ? (
            <TableRow>
              <TableCell
                colSpan={6}
                className="text-muted-foreground text-center"
              >
                Sin secuencias registradas.
              </TableCell>
            </TableRow>
          ) : (
            sequences.map((sequence) => (
              <TableRow key={sequence.id}>
                <TableCell>{sequence.environment}</TableCell>
                <TableCell>{sequence.type}</TableCell>
                <TableCell>{sequence.series}</TableCell>
                <TableCell>
                  {sequence.rangeFrom}–{sequence.rangeTo}
                </TableCell>
                <TableCell>
                  {formatCount(Number(sequence.remaining))}
                  {sequence.isLowStock && (
                    <Badge variant="destructive" className="ml-2">
                      Bajo
                    </Badge>
                  )}
                </TableCell>
                <TableCell>
                  <Badge variant={sequence.active ? "outline" : "secondary"}>
                    {sequence.active ? "Activo" : "Inactivo"}
                  </Badge>
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
  environment: z.string().min(1, "El ambiente es obligatorio."),
  type: z.string().min(1, "El tipo de e-CF es obligatorio."),
  series: z
    .string()
    .length(1, "La serie es una sola letra.")
    .regex(/^[E-Z]$/i, "La serie va de E a Z, salvo P.")
    .refine(
      (value) => value.toUpperCase() !== "P",
      "La serie va de E a Z, salvo P.",
    ),
  rangeFrom: z.string().min(1, "El inicio del rango es obligatorio."),
  rangeTo: z.string().min(1, "El final del rango es obligatorio."),
});

type Values = z.infer<typeof schema>;

function RegisterSequenceDialog({ tenantId }: { tenantId: string }) {
  const [open, setOpen] = useState(false);
  const register = useRegisterSequence();

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      environment: "Test",
      type: "31",
      series: "E",
      rangeFrom: "1",
      rangeTo: "100",
    },
  });
  const environment = useController({
    control: form.control,
    name: "environment",
  });
  const type = useController({ control: form.control, name: "type" });

  const submit = form.handleSubmit(async (values) => {
    try {
      await register.mutateAsync({
        tenantId,
        environment: values.environment,
        type: Number(values.type),
        series: values.series.toUpperCase(),
        rangeFrom: Number(values.rangeFrom),
        rangeTo: Number(values.rangeTo),
      });
      form.reset();
      setOpen(false);
    } catch (error) {
      if (!applyFieldErrors(error, form.setError)) {
        form.setError("rangeTo", { message: tenantErrorMessage(error) });
      }
    }
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) form.reset();
      }}
    >
      <DialogTrigger render={<Button size="xs" />}>
        <Plus /> Registrar rango
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Registrar rango de e-NCF</DialogTitle>
          <DialogDescription>
            El rango autorizado por la DGII para este tipo de comprobante.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
          <Field>
            <FieldLabel htmlFor="sequence-environment">Ambiente</FieldLabel>
            <Select
              items={selectItems(ENVIRONMENT_OPTIONS)}
              value={environment.field.value}
              onValueChange={(next) => environment.field.onChange(String(next))}
            >
              <SelectTrigger id="sequence-environment" className="w-full">
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

          <Field>
            <FieldLabel htmlFor="sequence-type">Tipo de e-CF</FieldLabel>
            <Select
              items={selectItems(ECF_TYPE_OPTIONS)}
              value={type.field.value}
              onValueChange={(next) => type.field.onChange(String(next))}
            >
              <SelectTrigger id="sequence-type" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ECF_TYPE_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <FieldError errors={[form.formState.errors.type]} />
          </Field>

          <Field>
            <FieldLabel htmlFor="sequence-series">
              Serie (1 letra, E–Z salvo P)
            </FieldLabel>
            <Input
              id="sequence-series"
              maxLength={1}
              className="uppercase"
              {...form.register("series")}
            />
            <FieldError errors={[form.formState.errors.series]} />
          </Field>

          <div className="grid grid-cols-2 gap-4">
            <Field>
              <FieldLabel htmlFor="sequence-from">Desde</FieldLabel>
              <Input
                id="sequence-from"
                type="number"
                min={1}
                {...form.register("rangeFrom")}
              />
              <FieldError errors={[form.formState.errors.rangeFrom]} />
            </Field>

            <Field>
              <FieldLabel htmlFor="sequence-to">Hasta</FieldLabel>
              <Input
                id="sequence-to"
                type="number"
                min={1}
                {...form.register("rangeTo")}
              />
              <FieldError errors={[form.formState.errors.rangeTo]} />
            </Field>
          </div>

          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>
              Cancelar
            </DialogClose>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              Registrar
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
