"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { authClient } from "@/lib/auth/client";

import { AuthCard } from "../auth-card";

interface ResetPasswordFormProps {
  token: string;
}

const schema = z
  .object({
    newPassword: z
      .string()
      .min(10, "La contraseña debe tener al menos 10 caracteres."),
    confirmPassword: z.string().min(1, "Confirmá la contraseña."),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "Las contraseñas no coinciden.",
    path: ["confirmPassword"],
  });

type Values = z.infer<typeof schema>;

export function ResetPasswordForm({ token }: ResetPasswordFormProps) {
  const router = useRouter();

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { newPassword: "", confirmPassword: "" },
  });

  const submit = form.handleSubmit(async ({ newPassword }) => {
    const result = await authClient.resetPassword({ newPassword, token });

    if (result.error) {
      form.setError("confirmPassword", {
        message:
          result.error.message ??
          "No se pudo restablecer la contraseña. Pedí un enlace nuevo.",
      });
      return;
    }

    router.push("/login?reset=1");
  });

  return (
    <AuthCard
      title="Elegí una contraseña nueva"
      description="Tiene que tener al menos 10 caracteres."
    >
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        <Field>
          <FieldLabel htmlFor="newPassword">Contraseña nueva</FieldLabel>
          <Input
            id="newPassword"
            type="password"
            autoComplete="new-password"
            {...form.register("newPassword")}
          />
          <FieldError errors={[form.formState.errors.newPassword]} />
        </Field>

        <Field>
          <FieldLabel htmlFor="confirmPassword">
            Confirmá la contraseña
          </FieldLabel>
          <Input
            id="confirmPassword"
            type="password"
            autoComplete="new-password"
            {...form.register("confirmPassword")}
          />
          <FieldError errors={[form.formState.errors.confirmPassword]} />
        </Field>

        <Button type="submit" disabled={form.formState.isSubmitting}>
          Restablecer contraseña
        </Button>
      </form>

      <Link
        href="/forgot-password"
        className="text-muted-foreground hover:text-foreground text-center text-sm"
      >
        Pedir un enlace nuevo
      </Link>
    </AuthCard>
  );
}
