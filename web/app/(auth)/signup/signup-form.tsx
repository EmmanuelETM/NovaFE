"use client";

import { useState } from "react";
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

interface SignupFormProps {
  next: string;
}

const schema = z
  .object({
    name: z.string().min(1, "El nombre es obligatorio."),
    email: z
      .string()
      .min(1, "El correo es obligatorio.")
      .email("Correo inválido."),
    password: z
      .string()
      .min(10, "La contraseña debe tener al menos 10 caracteres."),
    confirmPassword: z.string().min(1, "Confirma la contraseña."),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: "Las contraseñas no coinciden.",
    path: ["confirmPassword"],
  });

type Values = z.infer<typeof schema>;

/**
 * Registro con email/contraseña. Crea la cuenta de Better Auth — el enlace con
 * el `platform_users` provisionado por un operador (por correo) se hace en el
 * primer login, no acá. Con `requireEmailVerification: true`, esto no abre
 * sesión: hay que verificar el correo primero.
 */
export function SignupForm({ next }: SignupFormProps) {
  const router = useRouter();
  const [awaitingVerification, setAwaitingVerification] = useState<
    string | null
  >(null);

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", email: "", password: "", confirmPassword: "" },
  });

  const submit = form.handleSubmit(async ({ name, email, password }) => {
    const result = await authClient.signUp.email({
      name,
      email,
      password,
      callbackURL: next,
    });

    if (result.error) {
      form.setError("email", {
        message: result.error.message ?? "No se pudo crear la cuenta.",
      });
      return;
    }

    if (result.data.token) {
      router.push(next);
      return;
    }

    setAwaitingVerification(email);
  });

  if (awaitingVerification) {
    return (
      <AuthCard title="Revisa tu correo" description="Ya casi terminas.">
        <p className="text-center text-sm">
          Te enviamos un enlace de verificación a{" "}
          <span className="font-medium">{awaitingVerification}</span>. Ábrelo
          para activar tu cuenta.
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
      title="Crea tu cuenta"
      description="Usa el correo con el que te dieron de alta en NovaFE."
    >
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        <Field>
          <FieldLabel htmlFor="name">Nombre</FieldLabel>
          <Input id="name" autoComplete="name" {...form.register("name")} />
          <FieldError errors={[form.formState.errors.name]} />
        </Field>

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

        <Field>
          <FieldLabel htmlFor="password">Contraseña</FieldLabel>
          <Input
            id="password"
            type="password"
            autoComplete="new-password"
            {...form.register("password")}
          />
          <FieldError errors={[form.formState.errors.password]} />
        </Field>

        <Field>
          <FieldLabel htmlFor="confirmPassword">
            Confirma la contraseña
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
          Crear cuenta
        </Button>
      </form>

      <p className="text-muted-foreground text-center text-sm">
        ¿Ya tienes cuenta?{" "}
        <Link
          href="/login"
          className="text-foreground underline underline-offset-4"
        >
          Inicia sesión
        </Link>
      </p>
    </AuthCard>
  );
}
