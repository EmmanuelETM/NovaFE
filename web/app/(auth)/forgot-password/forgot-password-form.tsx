"use client";

import { useState } from "react";
import Link from "next/link";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { authClient } from "@/lib/auth/client";

import { AuthCard } from "../auth-card";

const schema = z.object({
  email: z
    .string()
    .min(1, "El correo es obligatorio.")
    .email("Correo inválido."),
});

type Values = z.infer<typeof schema>;

/**
 * Pide el correo para restablecer la contraseña. Better Auth siempre responde
 * con éxito acá, exista o no la cuenta — no hay que distinguir esa rama de
 * error, así no se puede usar esta pantalla para averiguar qué correos están
 * registrados.
 */
export function ForgotPasswordForm() {
  const [sentTo, setSentTo] = useState<string | null>(null);

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { email: "" },
  });

  const submit = form.handleSubmit(async ({ email }) => {
    await authClient.requestPasswordReset({
      email,
      redirectTo: "/reset-password",
    });
    setSentTo(email);
  });

  if (sentTo) {
    return (
      <AuthCard
        title="Revisa tu correo"
        description="Si esa cuenta existe, ya le enviamos un enlace."
      >
        <p className="text-center text-sm">
          Si <span className="font-medium">{sentTo}</span> tiene una cuenta, te
          enviamos un enlace para restablecer la contraseña.
        </p>
        <Link
          href="/login"
          className="text-foreground text-center text-sm underline underline-offset-4"
        >
          Volver a iniciar sesión
        </Link>
      </AuthCard>
    );
  }

  return (
    <AuthCard
      title="¿Olvidaste tu contraseña?"
      description="Te enviamos un enlace para elegir una nueva."
    >
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        <Field>
          <FieldLabel htmlFor="email">Correo</FieldLabel>
          <Input
            id="email"
            type="email"
            autoComplete="email"
            {...form.register("email")}
          />
          <FieldError errors={[form.formState.errors.email]} />
        </Field>

        <Button type="submit" disabled={form.formState.isSubmitting}>
          Enviar enlace
        </Button>
      </form>

      <Link
        href="/login"
        className="text-muted-foreground hover:text-foreground text-center text-sm"
      >
        Volver a iniciar sesión
      </Link>
    </AuthCard>
  );
}
