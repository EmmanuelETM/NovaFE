"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useController, useForm } from "react-hook-form";
import { z } from "zod";
import { MoreHorizontal, Plus } from "lucide-react";

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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Progress } from "@/components/ui/progress";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { applyFieldErrors } from "@/lib/api/form-errors";
import {
  ECF_TYPE_OPTIONS,
  ENVIRONMENT_OPTIONS,
} from "@/features/tenants/options";
import { formatCount } from "@/lib/format";
import { selectItems } from "@/lib/select-items";
import { cn } from "@/lib/utils";

import { tenantConfigErrorMessage } from "./use-certificates";
import {
  useDeactivateSequence,
  useRegisterSequence,
  useSequences,
} from "./use-sequences";
import type { NcfSequence } from "./types";

const ch = createAppColumnHelper<NcfSequence>();

const columns = ch.columns([
  ch.display({
    id: "encf",
    header: "Serie",
    cell: (cell) => (
      <span className="font-mono font-medium">
        {cell.row.original.series}
        {String(cell.row.original.type).padStart(2, "0")}
      </span>
    ),
  }),
  ch.accessor("typeName", { header: "Nombre" }),
  ch.display({
    id: "rango",
    header: "Rango",
    cell: (cell) => (
      <div className="flex flex-col font-mono text-xs">
        <span>{cell.row.original.rangeFrom}</span>
        <span className="text-muted-foreground">
          {cell.row.original.rangeTo}
        </span>
      </div>
    ),
  }),
  ch.display({
    id: "consumo",
    header: "Consumo",
    cell: (cell) => <SequenceProgress sequence={cell.row.original} />,
  }),
  ch.accessor("environment", { header: "Ambiente" }),
  ch.display({
    id: "vencimiento",
    header: "Vencimiento",
    // `expiresOn` ya viaja como `dd-MM-yyyy` (`DateOnlyJsonConverter`, formato
    // DGII) — se muestra tal cual, sin reformatear.
    cell: (cell) => cell.row.original.expiresOn ?? "—",
  }),
  ch.display({
    id: "estado",
    header: "Estado",
    cell: (cell) => <SequenceStatus sequence={cell.row.original} />,
  }),
  ch.display({
    id: "acciones",
    header: "",
    meta: { align: "right" },
    cell: (cell) => <SequenceActions sequence={cell.row.original} />,
  }),
]);

export function SequencesScreen() {
  const { data: sequences, isPending, error } = useSequences();

  return (
    <DataTable
      columns={columns}
      page={toStaticPage(sequences)}
      isPending={isPending}
      error={error}
      state={STATIC_TABLE_STATE}
      onStateChange={() => {}}
      searchable={false}
      emptyState={{ title: "Sin secuencias registradas." }}
      toolbarActions={<RegisterSequenceDialog />}
      getRowId={(sequence) => sequence.id}
    />
  );
}

/** La barra de consumo: cuánto queda del rango, achicándose hasta agotarse. */
function SequenceProgress({ sequence }: { sequence: NcfSequence }) {
  const capacity = Number(sequence.capacity);
  const remaining = Number(sequence.remaining);
  const exhausted = remaining <= 0;

  return (
    <div className="flex w-56 flex-col gap-1.5">
      <div className="flex items-baseline justify-between gap-2 text-xs">
        <span className="font-medium tabular-nums">
          {formatCount(remaining)}
        </span>
        <span className="text-muted-foreground tabular-nums">
          de {formatCount(capacity)}
        </span>
      </div>
      <Progress
        value={remaining}
        max={capacity}
        className={cn(
          exhausted && "[&_[data-slot=progress-indicator]]:bg-destructive",
          !exhausted &&
            sequence.isLowStock &&
            "[&_[data-slot=progress-indicator]]:bg-amber-500",
        )}
      />
    </div>
  );
}

function SequenceStatus({ sequence }: { sequence: NcfSequence }) {
  const exhausted = Number(sequence.remaining) <= 0;

  return (
    <div className="flex flex-wrap gap-1.5">
      <Badge variant={sequence.active ? "outline" : "secondary"}>
        {sequence.active ? "Activo" : "Inactivo"}
      </Badge>
      {exhausted && <Badge variant="destructive">Agotado</Badge>}
      {!exhausted && sequence.isLowStock && (
        <Badge className="bg-amber-500/15 text-amber-600 dark:text-amber-400">
          Quedan pocos
        </Badge>
      )}
    </div>
  );
}

function SequenceActions({ sequence }: { sequence: NcfSequence }) {
  const deactivate = useDeactivateSequence();

  if (!sequence.active) return null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={<Button variant="ghost" size="xs" aria-label="Acciones" />}
      >
        <MoreHorizontal />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem
          disabled={deactivate.isPending}
          onClick={() => deactivate.mutate(sequence.id)}
        >
          Desactivar
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
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

function RegisterSequenceDialog() {
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
        form.setError("rangeTo", { message: tenantConfigErrorMessage(error) });
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
            El rango que te autorizó la DGII para este tipo de comprobante. Si
            ya tenías uno agotado con la misma serie, desactívalo primero.
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
