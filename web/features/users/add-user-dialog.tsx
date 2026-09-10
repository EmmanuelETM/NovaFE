"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useController, useForm } from "react-hook-form";
import { z } from "zod";
import { UserPlus } from "lucide-react";

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
import { selectItems } from "@/lib/select-items";

import { TENANT_ROLE_OPTIONS } from "./role-options";
import { userErrorMessage } from "./use-users";

const schema = z.object({
  email: z
    .string()
    .min(1, "El correo es obligatorio.")
    .email("El correo no tiene un formato válido."),
  role: z.string().min(1, "El rol es obligatorio."),
});

type Values = z.infer<typeof schema>;

interface AddUserDialogProps {
  /** Con rol (usuario de contribuyente) o sin rol (operador del SaaS). */
  withRole: boolean;
  label: string;
  onCreate: (input: { email: string; role: string }) => Promise<unknown>;
}

export function AddUserDialog({
  withRole,
  label,
  onCreate,
}: AddUserDialogProps) {
  const [open, setOpen] = useState(false);

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", role: "emisor" },
  });

  const role = useController({ control: form.control, name: "role" });

  const submit = form.handleSubmit(async (values) => {
    try {
      await onCreate({ email: values.email, role: values.role });
      form.reset();
      setOpen(false);
    } catch (error) {
      if (!applyFieldErrors(error, form.setError)) {
        form.setError("email", { message: userErrorMessage(error) });
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
        <UserPlus /> {label}
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>{label}</DialogTitle>
          <DialogDescription>
            El alta es por correo. La persona entra cuando inicia sesión con ese
            mismo correo.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
          <Field>
            <FieldLabel htmlFor="add-user-email">Correo</FieldLabel>
            <Input
              id="add-user-email"
              type="email"
              autoComplete="off"
              {...form.register("email")}
            />
            <FieldError errors={[form.formState.errors.email]} />
          </Field>

          {withRole && (
            <Field>
              <FieldLabel htmlFor="add-user-role">Rol</FieldLabel>
              <Select
                items={selectItems(TENANT_ROLE_OPTIONS)}
                value={role.field.value}
                onValueChange={(next) => role.field.onChange(String(next))}
              >
                <SelectTrigger id="add-user-role" className="w-full">
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
          )}

          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>
              Cancelar
            </DialogClose>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              Dar de alta
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
