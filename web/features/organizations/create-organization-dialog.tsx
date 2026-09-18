"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useController, useForm } from "react-hook-form";
import { z } from "zod";
import { Plus } from "lucide-react";

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
import { PLAN_OPTIONS } from "@/features/tenants/options";
import { applyFieldErrors } from "@/lib/api/form-errors";
import { selectItems } from "@/lib/select-items";

import {
  organizationErrorMessage,
  useRegisterOrganization,
} from "./use-organizations";

/** `Acme Contadores` → `acme-contadores`. Mismo formato que valida la API: minúsculas, dígitos y guiones. */
function slugify(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

const schema = z.object({
  name: z.string().min(1, "El nombre es obligatorio."),
  slug: z
    .string()
    .min(1, "El slug es obligatorio.")
    .regex(
      /^[a-z0-9]+(-[a-z0-9]+)*$/,
      "Solo minúsculas, dígitos y guiones (sin empezar ni terminar en guion).",
    ),
  plan: z.string().min(1, "El plan es obligatorio."),
  ownerEmail: z.email("Correo inválido.").or(z.literal("")),
});

type Values = z.infer<typeof schema>;

export function CreateOrganizationDialog() {
  const [open, setOpen] = useState(false);
  const [slugTouched, setSlugTouched] = useState(false);
  const register = useRegisterOrganization();

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", slug: "", plan: "Developer", ownerEmail: "" },
  });
  const plan = useController({ control: form.control, name: "plan" });

  const submit = form.handleSubmit(async (values) => {
    try {
      await register.mutateAsync({
        name: values.name.trim(),
        slug: values.slug.trim(),
        plan: values.plan,
        ownerEmail: values.ownerEmail.trim() || undefined,
      });
      setOpen(false);
      form.reset();
      setSlugTouched(false);
    } catch (error) {
      if (!applyFieldErrors(error, form.setError)) {
        form.setError("name", { message: organizationErrorMessage(error) });
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
          setSlugTouched(false);
        }
      }}
    >
      <DialogTrigger render={<Button size="sm" className="h-8 gap-1.5" />}>
        <Plus /> Nueva organización
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Nueva organización</DialogTitle>
          <DialogDescription>
            El dueño es opcional: si pones un correo que todavía no es usuario
            de la plataforma, se da de alta en el mismo paso.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
          <Field>
            <FieldLabel htmlFor="org-name">Nombre</FieldLabel>
            <Input
              id="org-name"
              {...form.register("name")}
              onChange={(event) => {
                form.setValue("name", event.target.value);
                if (!slugTouched) {
                  form.setValue("slug", slugify(event.target.value));
                }
              }}
            />
            <FieldError errors={[form.formState.errors.name]} />
          </Field>

          <Field>
            <FieldLabel htmlFor="org-slug">Slug</FieldLabel>
            <Input
              id="org-slug"
              className="font-mono"
              {...form.register("slug")}
              onChange={(event) => {
                setSlugTouched(true);
                form.setValue("slug", event.target.value);
              }}
            />
            <FieldError errors={[form.formState.errors.slug]} />
          </Field>

          <Field>
            <FieldLabel htmlFor="org-plan">Plan</FieldLabel>
            <Select
              items={selectItems(PLAN_OPTIONS)}
              value={plan.field.value}
              onValueChange={(next) => plan.field.onChange(String(next))}
            >
              <SelectTrigger id="org-plan" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {PLAN_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>

          <Field>
            <FieldLabel htmlFor="org-owner-email">
              Correo del dueño (opcional)
            </FieldLabel>
            <Input
              id="org-owner-email"
              type="email"
              autoComplete="off"
              {...form.register("ownerEmail")}
            />
            <FieldError errors={[form.formState.errors.ownerEmail]} />
          </Field>

          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>
              Cancelar
            </DialogClose>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              Crear
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
