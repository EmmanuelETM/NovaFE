"use client";

import { useState } from "react";
import { Boxes } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { authClient } from "@/lib/auth/client";
import { SOCIAL_PROVIDER_LABELS, type SocialProvider } from "@/lib/auth/social";

import { ProviderMark } from "./provider-mark";

interface LoginFormProps {
  providers: SocialProvider[];
  next: string;
}

/**
 * El acceso al dashboard. Solo OAuth por ahora — un botón por cada provider con
 * credenciales configuradas. Better Auth redirige al provider y vuelve a `next`.
 */
export function LoginForm({ providers, next }: LoginFormProps) {
  const [pending, setPending] = useState<SocialProvider | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function signIn(provider: SocialProvider) {
    setError(null);
    setPending(provider);
    const result = await authClient.signIn.social({
      provider,
      callbackURL: next,
      errorCallbackURL: "/auth-error",
    });
    // Si redirige, esta línea no se alcanza. Si devuelve un error, sí.
    if (result.error) {
      setPending(null);
      setError(result.error.message ?? "No se pudo iniciar sesión.");
    }
  }

  return (
    <Card className="w-full max-w-sm">
      <CardHeader className="items-center text-center">
        <div className="bg-primary text-primary-foreground mb-2 flex size-11 items-center justify-center rounded-xl">
          <Boxes className="size-6" aria-hidden />
        </div>
        <CardTitle className="text-lg">Entrar a NovaFE</CardTitle>
        <CardDescription>
          Usá la cuenta con la que te dieron de alta.
        </CardDescription>
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        {providers.length === 0 ? (
          <p className="text-muted-foreground text-center text-sm">
            No hay ningún método de acceso configurado todavía.
          </p>
        ) : (
          providers.map((provider) => (
            <Button
              key={provider}
              variant="outline"
              className="w-full justify-center gap-2.5"
              disabled={pending !== null}
              onClick={() => void signIn(provider)}
            >
              <ProviderMark provider={provider} />
              {pending === provider
                ? "Redirigiendo…"
                : `Continuar con ${SOCIAL_PROVIDER_LABELS[provider]}`}
            </Button>
          ))
        )}

        {error && (
          <p className="text-destructive text-center text-sm" role="alert">
            {error}
          </p>
        )}
      </CardContent>
    </Card>
  );
}
