"use client";

import { useState } from "react";
import Link from "next/link";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import {
  Field,
  FieldError,
  FieldLabel,
  FieldSeparator,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { authClient } from "@/lib/auth/client";
import { SOCIAL_PROVIDER_LABELS, type SocialProvider } from "@/lib/auth/social";

import { AuthCard } from "../auth-card";
import { ProviderMark } from "./provider-mark";

interface LoginFormProps {
  providers: SocialProvider[];
  next: string;
  /** Venimos de restablecer la contraseña con éxito (`/login?reset=1`). */
  justReset: boolean;
}

const schema = z.object({
  email: z
    .string()
    .min(1, "El correo es obligatorio.")
    .email("Correo inválido."),
  password: z.string().min(1, "La contraseña es obligatoria."),
});

type Values = z.infer<typeof schema>;

/**
 * El acceso al dashboard: email/contraseña + OAuth (un botón por provider con
 * credenciales configuradas). Better Auth redirige al provider y vuelve a `next`.
 */
export function LoginForm({ providers, next, justReset }: LoginFormProps) {
  const [pendingProvider, setPendingProvider] = useState<SocialProvider | null>(
    null,
  );
  const [oauthError, setOauthError] = useState<string | null>(null);
  const [unverifiedEmail, setUnverifiedEmail] = useState<string | null>(null);
  const [resendState, setResendState] = useState<"idle" | "sending" | "sent">(
    "idle",
  );

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", password: "" },
  });

  const submit = form.handleSubmit(async ({ email, password }) => {
    setUnverifiedEmail(null);
    const result = await authClient.signIn.email({
      email,
      password,
      callbackURL: next,
    });
    // Si redirige, esta línea no se alcanza. Si devuelve un error, sí.
    if (result.error) {
      if (result.error.code === "EMAIL_NOT_VERIFIED") {
        setUnverifiedEmail(email);
        return;
      }
      form.setError("password", {
        message: "Correo o contraseña incorrectos.",
      });
    }
  });

  async function resendVerification() {
    if (!unverifiedEmail) return;
    setResendState("sending");
    await authClient.sendVerificationEmail({
      email: unverifiedEmail,
      callbackURL: next,
    });
    setResendState("sent");
  }

  async function signInWithProvider(provider: SocialProvider) {
    setOauthError(null);
    setPendingProvider(provider);
    const result = await authClient.signIn.social({
      provider,
      callbackURL: next,
      errorCallbackURL: "/auth-error",
    });
    if (result.error) {
      setPendingProvider(null);
      setOauthError(result.error.message ?? "No se pudo iniciar sesión.");
    }
  }

  return (
    <AuthCard
      title="Entrar a NovaFE"
      description="Usá la cuenta con la que te dieron de alta."
    >
      {justReset && (
        <p className="text-center text-sm text-emerald-600 dark:text-emerald-400">
          Contraseña actualizada. Iniciá sesión con tu contraseña nueva.
        </p>
      )}

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

        <Field>
          <div className="flex items-center justify-between">
            <FieldLabel htmlFor="password">Contraseña</FieldLabel>
            <Link
              href="/forgot-password"
              className="text-muted-foreground hover:text-foreground text-xs"
            >
              ¿Olvidaste tu contraseña?
            </Link>
          </div>
          <Input
            id="password"
            type="password"
            autoComplete="current-password"
            {...form.register("password")}
          />
          <FieldError errors={[form.formState.errors.password]} />
        </Field>

        {unverifiedEmail && (
          <div className="bg-muted rounded-lg p-3 text-sm">
            <p>Tenés que verificar tu correo antes de entrar.</p>
            <Button
              type="button"
              variant="link"
              className="h-auto p-0 text-sm"
              disabled={resendState !== "idle"}
              onClick={() => void resendVerification()}
            >
              {resendState === "sent"
                ? "Te reenviamos el correo de verificación."
                : resendState === "sending"
                  ? "Enviando…"
                  : "Reenviar correo de verificación"}
            </Button>
          </div>
        )}

        <Button type="submit" disabled={form.formState.isSubmitting}>
          Iniciar sesión
        </Button>
      </form>

      <p className="text-muted-foreground text-center text-sm">
        ¿No tenés cuenta?{" "}
        <Link
          href="/signup"
          className="text-foreground underline underline-offset-4"
        >
          Creá una
        </Link>
      </p>

      {providers.length > 0 && (
        <>
          <FieldSeparator>o</FieldSeparator>

          <div className="flex flex-col gap-3">
            {providers.map((provider) => (
              <Button
                key={provider}
                variant="outline"
                className="w-full justify-center gap-2.5"
                disabled={pendingProvider !== null}
                onClick={() => void signInWithProvider(provider)}
              >
                <ProviderMark provider={provider} />
                {pendingProvider === provider
                  ? "Redirigiendo…"
                  : `Continuar con ${SOCIAL_PROVIDER_LABELS[provider]}`}
              </Button>
            ))}
          </div>

          {oauthError && (
            <p className="text-destructive text-center text-sm" role="alert">
              {oauthError}
            </p>
          )}
        </>
      )}
    </AuthCard>
  );
}
